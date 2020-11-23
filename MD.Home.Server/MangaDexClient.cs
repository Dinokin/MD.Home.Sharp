using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MD.Home.Server.Configuration;
using MD.Home.Server.Serialization;
using Serilog;
using Constants = MD.Home.Server.Others.Constants;

namespace MD.Home.Server
{
    public class MangaDexClient
    {
        public ClientSettings ClientSettings { get; }
        public JsonSerializerOptions JsonSerializerOptions { get; }
        public HttpClient HttpClient { get; }

        public RemoteSettings RemoteSettings
        {
            get
            {
                if (_remoteSettings == null)
                    throw new InvalidOperationException("This client has not logged in to the control.");

                return _remoteSettings;
            }
        }
        
        private readonly ILogger _logger;

        private RemoteSettings? _remoteSettings;
        private Task? _backgroundTask;
        private bool _canPing;

        public MangaDexClient(ClientSettings clientSettings, HttpClient httpClient, ILogger logger)
        {
            var snakePolicy = new SnakeCaseNamingPolicy();
            
            ClientSettings = clientSettings;
            HttpClient = httpClient;
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = snakePolicy,
                DictionaryKeyPolicy = snakePolicy
            };
            _logger = logger;
            
            HttpClient.Timeout = TimeSpan.FromSeconds(90);
        }

        public async Task LoginToControl()
        {
            if (_remoteSettings != null)
                throw new InvalidOperationException();
            
            _logger.Information("Connecting to the control server");

            var message = JsonSerializer.Serialize(GetPingParameters());
            var response = await HttpClient.PostAsync($"{Constants.ServerAddress}ping", new StringContent(message, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                _remoteSettings = JsonSerializer.Deserialize<RemoteSettings>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions {PropertyNamingPolicy = new SnakeCaseNamingPolicy()});
                _canPing = true;
                
                if (_remoteSettings?.LatestBuild > Constants.ClientBuild)
                    _logger.Warning($"Outdated build detected! Latest: {_remoteSettings.LatestBuild}, Current: {Constants.ClientBuild}");
                
                SetupBackgroundTask();
            }
            else
                throw new AuthenticationException();
        }

        public async Task LogoutFromControl()
        {
            if (_remoteSettings == null)
                throw new InvalidOperationException("This client has not logged in to the control.");
            
            _logger.Information("Disconnecting from the control server");

            var message = JsonSerializer.Serialize(new Dictionary<string, object> { {"secret", ClientSettings.ClientSecret} });
            var response = await HttpClient.PostAsync($"{Constants.ServerAddress}stop", new StringContent(message, Encoding.UTF8, "application/json"));

            _canPing = false;
            
            if (!response.IsSuccessStatusCode)
                throw new AuthenticationException();
        }

        private async Task PingControl()
        {
            if (_remoteSettings == null)
                throw new InvalidOperationException();
            
            _logger.Information("Pinging the control server");
            
            var message = JsonSerializer.Serialize(GetPingParameters());
            HttpResponseMessage response;

            try
            {
                response = await HttpClient.PostAsync($"{Constants.ServerAddress}ping", new StringContent(message, Encoding.UTF8, "application/json"));
            }
            catch
            {
                _logger.Error("Failed to ping control due to unknown reasons");

                return;
            }

            if (response.IsSuccessStatusCode)
            {
                var remoteSettings = JsonSerializer.Deserialize<RemoteSettings>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions);
                
                _logger.Information($"Server settings received: {remoteSettings}");

                if (remoteSettings?.LatestBuild > Constants.ClientBuild)
                    _logger.Warning($"Outdated build detected! Latest: {remoteSettings.LatestBuild}, Current: {Constants.ClientBuild}");

                if (RemoteSettings.TlsCertificate?.Certificate != remoteSettings?.TlsCertificate?.Certificate)
                {
                    _logger.Information("Restarting ImageServer to refresh certificates");
                    
                    Program.Restart();
                }

                _remoteSettings = remoteSettings;
            }
            else
                _logger.Information("Server ping failed - ignoring");
        }
        
        private Dictionary<string, object> GetPingParameters()
        {
            var message = new Dictionary<string, object>
            {
                {"secret", ClientSettings.ClientSecret},
                {"port", ClientSettings.ClientExternalPort != 0 ? ClientSettings.ClientExternalPort : ClientSettings.ClientPort},
                {"disk_space", ClientSettings.MaxCacheSizeInMebibytes * 1024 * 1024},
                {"network_speed", 0},
                {"build_version", Constants.ClientBuild}
            };
            
            if (_remoteSettings?.TlsCertificate != null)
                message.Add("tls_created_at", _remoteSettings.TlsCertificate.CreatedAt);

            return message;
        }

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private void SetupBackgroundTask()
        {
            _backgroundTask = new Task(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));

                    if (_canPing)
                        await PingControl();
                }
            }, TaskCreationOptions.LongRunning);
            
            _backgroundTask.Start();
        }
    }
}