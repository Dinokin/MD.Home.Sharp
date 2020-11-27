using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using MD.Home.Sharp.Configuration;
using MD.Home.Sharp.Exceptions;
using MD.Home.Sharp.Extensions;
using MD.Home.Sharp.Others;
using MD.Home.Sharp.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Constants = MD.Home.Sharp.Others.Constants;

namespace MD.Home.Sharp
{
    internal static class Program
    {
        public static readonly MangaDexClient MangaDexClient;
        public static readonly HttpClient HttpClient;
        
        private static readonly IConfiguration? Configuration;
        private static readonly ILogger Logger;

        private static readonly ClientSettings ClientSettings;
        private static readonly JsonSerializerOptions SerializerOptions;

        private static bool _stopRequested;
        private static bool _restartRequested;

        static Program()
        {
            var namingPolicy = new SnakeCaseNamingPolicy();

            ClientSettings = new ClientSettings();
            SerializerOptions = new JsonSerializerOptions {PropertyNamingPolicy = namingPolicy, DictionaryKeyPolicy = namingPolicy};
            Configuration = new ConfigurationBuilder().AddJsonFile(Constants.SettingsFile, false).Build();
            Configuration.Bind(ClientSettings);
            HttpClient = new HttpClient(GetHttpClientHandler());

            ValidateSettings();
            
            Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .WriteTo.Console()
                .CreateLogger();

            Console.CancelKeyPress += (_, _) => _stopRequested = true;

            MangaDexClient = new MangaDexClient(Logger, ClientSettings, SerializerOptions);
        }

        public static async Task Main()
        {
            await MangaDexClient.LoginToControl();
            
            Logger.Information("Starting image server");
            
            var host = GetImageServer();
            await host.StartAsync();

            while (!_stopRequested)
            {
                if (!_stopRequested && _restartRequested)
                {
                    await host.StopAsync();
                    host.Dispose();

                    host = GetImageServer();
                    await host.StartAsync();
                    
                    _restartRequested = false;
                }
                
                await Task.Delay(10);
            }

            await MangaDexClient.LogoutFromControl();

            Logger.Information("Gracefully stopping image server");
            
            await Task.Delay(TimeSpan.FromSeconds(ClientSettings.GracefulShutdownWaitSeconds));
            await host.StopAsync();
            
            host.Dispose();
            MangaDexClient.Dispose();
            HttpClient.Dispose();
        }
        
        public static void Restart() => _restartRequested = true;

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

        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        private static IHost GetImageServer() =>
            Host.CreateDefaultBuilder()
                .ConfigureHostConfiguration(builder => builder.AddConfiguration(Configuration, false))
                .UseSerilog((_, configuration) =>
                {
                    configuration
                        .Enrich.FromLogContext()
                        .MinimumLevel.Information()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
                        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                        .WriteTo.Console();
                })
                .ConfigureWebHostDefaults(builder =>
                {
                    builder.ConfigureKestrel(options =>
                    {
                        options.AddServerHeader = false;
                        
                        using var certificate = new X509Certificate2(Security.GetCertificateBytesFromBase64(MangaDexClient.RemoteSettings.TlsCertificate!.Certificate, Security.InputType.Certificate));
                        using var provider = new RSACryptoServiceProvider();

                        provider.ImportRSAPrivateKey(MemoryMarshal.AsBytes(Security.GetCertificateBytesFromBase64(MangaDexClient.RemoteSettings.TlsCertificate.PrivateKey, Security.InputType.PrivateKey).AsSpan()), out _);

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

                    builder.ConfigureServices(services =>
                    {
                        services.AddSingleton(typeof(ClientSettings), ClientSettings);
                        services.AddSingleton(typeof(JsonSerializerOptions), SerializerOptions);
                    });

                    builder.UseStartup<ImageServer>();
                }).Build();

        private static SocketsHttpHandler GetHttpClientHandler()
        {
            var httpHandler = new SocketsHttpHandler();

            httpHandler.ConnectCallback += async (context, token) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse(ClientSettings.ClientHostname), 0));
                
                await socket.ConnectAsync(context.DnsEndPoint, token);

                return new NetworkStream(socket, true);
            };

            return httpHandler;
        }
    }
}