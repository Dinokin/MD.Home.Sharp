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
                .WriteTo.File(Constants.ClientLogFile, fileSizeLimitBytes: 1024 * 1000 * 10, rollOnFileSizeLimit: true, shared: true, flushToDiskInterval: TimeSpan.FromSeconds(5))
                .CreateLogger();
            
            MangaDexClient = new MangaDexClient(ClientSettings, new HttpClient(), logger);

            Console.CancelKeyPress += (_, _) => _stopRequested = true;
        }

        public static async Task Main()
        {
            await MangaDexClient.LoginToControl();
            var host = GetImageServer();
            var hostTask = host.StartAsync(_cancellationTokenSource.Token);

            while (!_stopRequested)
            {
                if (hostTask.IsCompleted && !_stopRequested)
                {
                    host.Dispose();
                    host = GetImageServer();
                    hostTask = host.StartAsync(_cancellationTokenSource.Token);
                }
                
                await Task.Delay(10);
            }

            await host.StopAsync();
            host.Dispose();
            await MangaDexClient.LogoutFromControl();
            MangaDexClient.HttpClient.Dispose();
            _cancellationTokenSource.Dispose();
        }
        
        public static void Stop()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
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
                        .WriteTo.Console()
                        .WriteTo.File(Constants.ServerLogFile, fileSizeLimitBytes: 1024 * 1000 * 10, rollOnFileSizeLimit: true, shared: true, flushToDiskInterval: TimeSpan.FromSeconds(5));
                })
                .ConfigureWebHostDefaults(builder =>
                {
                    builder.ConfigureKestrel(options =>
                    {
                        options.AddServerHeader = false;

                        /*
                         * var certificate = new X509Certificate2(Security.GetCertificatesBytes(MangaDexClient.RemoteSettings.TlsCertificate.Certificate));
                         * Workaround for .NET bug https://github.com/dotnet/runtime/issues/23749
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
                                     * Workaround for .NET bug https://github.com/dotnet/runtime/issues/23749
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