using System;
using System.Diagnostics.CodeAnalysis;
using MD.Home.Server.Cache;
using MD.Home.Server.Filters;
using MD.Home.Server.Others;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MD.Home.Server
{
    [SuppressMessage("ReSharper", "CA1822")]
    public class ImageServer
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers(options =>
            {
                options.Filters.Add(typeof(HeaderInjector));
                options.Filters.Add(typeof(ExceptionFilter));
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
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} from {IPAddress} responded {StatusCode} in {Elapsed:0.0000} ms with TTFB in {TTFB:0.0000} ms";

                options.EnrichDiagnosticContext = (context, httpContext) =>
                {
                    var ttfbAvailable = httpContext.Items.TryGetValue("TTFB", out var value);
                    
                    context.Set("IPAddress", httpContext.Connection.RemoteIpAddress);
                    context.Set("TTFB", ttfbAvailable ? (double) value! : -1); 
                };
            });
            
            application.UseRouting();
            application.UseCors(builder => builder.WithOrigins("https://mangadex.org").WithHeaders("*").WithMethods("GET"));
            application.UseEndpoints(builder => builder.MapControllers());
        }
    }
}