using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MD.Home.Server.Cache;
using MD.Home.Server.Others;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Context;

namespace MD.Home.Server
{
    [SuppressMessage("ReSharper", "CA1822")]
    public class ImageServer
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton<CacheManager>();
        }

        public void Configure(IApplicationBuilder application, IWebHostEnvironment environment)
        {
            application.Use(async (context, func) =>
            {
                var startTime = DateTime.UtcNow;
                
                using var ipAddressProperty = LogContext.PushProperty("IPAddress", context.Connection.RemoteIpAddress);
                context.Response.Headers.Add("timing-allow-origin", "https://mangadex.org");
                context.Response.Headers.Add("Server", $"MD.Home.Sharp 1.0.0 {Constants.ClientBuild}");
                    
                await func();

                var timeTaken = (DateTime.UtcNow - startTime).TotalMilliseconds;
                context.Response.Headers.Add("X-Time-Taken", timeTaken.ToString(CultureInfo.InvariantCulture));
                using var timeTakeProperty = LogContext.PushProperty("TimeTaken", timeTaken);
            });
            application.UseSerilogRequestLogging(options => options.MessageTemplate = "HTTP {RequestPath} from {IPAddress} responded {StatusCode} in {TimeTaken:0.0000} ms");
            application.UseRouting();
            application.UseCors(builder => builder.WithOrigins("https://mangadex.org").WithHeaders("*").WithMethods("GET"));
            application.UseEndpoints(builder => builder.MapControllers());
        }
    }
}