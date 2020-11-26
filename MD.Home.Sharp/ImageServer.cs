using System;
using System.Diagnostics.CodeAnalysis;
using MD.Home.Sharp.Cache;
using MD.Home.Sharp.Extensions;
using MD.Home.Sharp.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MD.Home.Sharp
{
    [SuppressMessage("ReSharper", "CA1822")]
    public class ImageServer
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers(options =>
            {
                options.Filters.Add<ReferrerValidator>();
                options.Filters.Add<TokenValidator>();
                options.Filters.Add<HeaderInjector>();
            });

            services.AddSingleton<CacheManager>();
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
                    httpContext.Items.TryGetValue("StartTime", out var startTime);
                    httpContext.Items.TryGetValue("TimeTaken", out var timeTaken);

                    context.Set("TimeTaken", timeTaken ?? (DateTime.UtcNow - (DateTime) startTime!).TotalMilliseconds);
                    context.Set("FilteredPath", httpContext.Request.Path.Value.GetFilteredPath());
                    context.Set("IPAddress", httpContext.Connection.RemoteIpAddress);
                };
            });
            
            application.UseRouting();
            application.UseEndpoints(builder => builder.MapControllers());
        }
    }
}