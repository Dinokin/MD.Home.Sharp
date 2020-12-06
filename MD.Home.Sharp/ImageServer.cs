using System;
using System.Diagnostics.CodeAnalysis;
using MD.Home.Sharp.Cache;
using MD.Home.Sharp.Extensions;
using MD.Home.Sharp.Filters;
using MD.Home.Sharp.Others.Cache;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MD.Home.Sharp
{
    [SuppressMessage("ReSharper", "CA1822")]
    internal class ImageServer
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting(options => options.LowercaseUrls = true);
            
            services.AddControllers(options =>
            {
                options.Filters.Add<ReferrerValidator>();
                options.Filters.Add<TokenValidator>();
                options.Filters.Add<HeaderInjector>();
            });

            services.AddSingleton<CacheManager>();
            services.AddSingleton<CacheStats>();
        }

        public void Configure(IApplicationBuilder application, IWebHostEnvironment environment)
        {
            application.Use(async (context, func) =>
            {
                context.Items.Add("StartTime", DateTime.UtcNow);

                await func();
            });
            
            application.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {FilteredPath} from {IPAddress} responded {StatusCode} in {TimeTaken:0.0000} ms";

                options.EnrichDiagnosticContext = (context, httpContext) =>
                {
                    var startTime = httpContext.Items["StartTime"];
                    var timeTaken = httpContext.Items["TimeTaken"];

                    context.Set("TimeTaken", timeTaken ?? (DateTime.UtcNow - (DateTime) startTime!).TotalMilliseconds);
                    context.Set("FilteredPath", httpContext.Request.Path.Value.RemoveToken());
                    context.Set("IPAddress", httpContext.Connection.RemoteIpAddress);
                };
            });
            
            application.UseRouting();
            application.UseEndpoints(builder => builder.MapControllers());
        }
    }
}