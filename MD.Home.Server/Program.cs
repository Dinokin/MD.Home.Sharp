using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using MD.Home.Server.Configuration;
using MD.Home.Server.Exceptions;
using MD.Home.Server.Extensions;
using MD.Home.Server.Others;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Constants = MD.Home.Server.Others.Constants;

namespace MD.Home.Server
{
    public static class Program
    {
        private static readonly ClientSettings ClientSettings = new();
        private static readonly MangaDexClient MangaDexClient;
        private static readonly IConfiguration? Configuration;

        private static CancellationTokenSource _cancellationTokenSource = new();

        private static bool _stopRequested;
        private static bool _restartRequested;

        static Program()
        {
            Configuration = new ConfigurationBuilder().AddJsonFile(Constants.SettingsFile, false).Build();
            Configuration.Bind(ClientSettings);
            
            ValidateSettings();
            
            Logger logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .Enrich.FromLogContext()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .WriteTo.Console()
                .CreateLogger();
            
            MangaDexClient = new MangaDexClient(ClientSettings, new HttpClient(), logger);

            Console.CancelKeyPress += (_, _) => _stopRequested = true;
        }

        public static async Task Main()
        {
            await MangaDexClient.LoginToControl();

            var host = GetImageServer();
            await host.StartAsync();

            while (!_stopRequested)
            {
                if (!_stopRequested && _restartRequested)
                {
                    await MangaDexClient.LogoutFromControl();
                    await Task.Delay(TimeSpan.FromSeconds(MangaDexClient.ClientSettings.GracefulShutdownWaitSeconds));
                    await host.StopAsync();
                    host.Dispose();

                    await MangaDexClient.LoginToControl();
                    host = GetImageServer();
                    await host.StartAsync();
                    _restartRequested = false;
                }
                
                await Task.Delay(10);
            }

            await MangaDexClient.LogoutFromControl();
            await Task.Delay(TimeSpan.FromSeconds(MangaDexClient.ClientSettings.GracefulShutdownWaitSeconds));
            await host.StopAsync();
            host.Dispose();
            
            MangaDexClient.HttpClient.Dispose();
            _cancellationTokenSource.Dispose();
        }
        
        public static void Restart()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _restartRequested = true;
        }

        private static void ValidateSettings()
        {
            if (!ClientSettings.ClientSecret.IsValidSecret())
                throw new ClientSettingsException("API Secret is invalid, must be 52 alphanumeric characters");

            if (ClientSettings.ClientPort == 0)
                throw new ClientSettingsException("Invalid port number");

            if (Constants.ReservedPorts.Any(port => port == ClientSettings.ClientPort))
                throw new ClientSettingsException("Unsafe port number");

            if (ClientSettings.MaxCacheSizeInMebibytes < 1024)
                throw new ClientSettingsException("Invalid max cache size, must be >= 1024 MiB (1GiB)");
        }

        private static IHost GetImageServer() => 
            Host.CreateDefaultBuilder()
                .ConfigureHostConfiguration(builder => builder.AddConfiguration(Configuration, false))
                .UseSerilog((_, configuration) =>
                {
                    configuration
                        .ReadFrom.Configuration(Configuration)
                        .Enrich.FromLogContext()
                        .MinimumLevel.Information()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                        .WriteTo.Console();
                })
                .ConfigureWebHostDefaults(builder =>
                {
                    builder.ConfigureKestrel(options =>
                    {
                        options.AddServerHeader = false;

                        /*
                         * var certificate = new X509Certificate2(Security.GetCertificatesBytes(MangaDexClient.RemoteSettings.TlsCertificate.Certificate));
                         * Workaround for https://github.com/dotnet/runtime/issues/23749
                         */
                        using var certificate = new X509Certificate2(Security.GetCertificateBytesFromBase64(MangaDexClient.RemoteSettings!.TlsCertificate!.Certificate!));
                        using var provider = new RSACryptoServiceProvider();

                        provider.ImportRSAPrivateKey(MemoryMarshal.AsBytes(Security.GetCertificateBytesFromBase64(MangaDexClient.RemoteSettings!.TlsCertificate!.PrivateKey).AsSpan()), out _);

                        if (!string.IsNullOrWhiteSpace(ClientSettings.ClientHostname))
                            options.Listen(IPAddress.Parse(ClientSettings.ClientHostname), ClientSettings.ClientPort,
                                listenOptions =>
                                {
                                    /*
                                     * listenOptions.UseHttps(certificate);
                                     * Workaround for https://github.com/dotnet/runtime/issues/23749
                                     */
                                    listenOptions.UseHttps(new X509Certificate2(certificate.CopyWithPrivateKey(provider).Export(X509ContentType.Pkcs12)),
                                        adapterOptions => adapterOptions.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13);
                                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                                });
                    });

                    builder.ConfigureServices(services => services.AddSingleton(_ => MangaDexClient));
                    builder.UseStartup<ImageServer>();
                }).Build();
    }
}