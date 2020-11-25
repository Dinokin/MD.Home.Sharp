using System.Diagnostics.CodeAnalysis;
using MD.Home.Server.Cache;
using MD.Home.Server.Filters;
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
                options.Filters.Add<ReferrerValidator>();
                options.Filters.Add<TokenValidator>();
                options.Filters.Add<HeaderInjector>();
            });

            services.AddSingleton<CacheManager>();
        }

        public void Configure(IApplicationBuilder application, IWebHostEnvironment environment)
        {
            application.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} from {IPAddress} responded {StatusCode} in {Elapsed:0.0000} ms";

                options.EnrichDiagnosticContext = (context, httpContext) => context.Set("IPAddress", httpContext.Connection.RemoteIpAddress);
            });
            
            application.UseRouting();
            application.UseEndpoints(builder => builder.MapControllers());
        }
    }
}