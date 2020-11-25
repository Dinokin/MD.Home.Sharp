﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MD.Home.Server.Configuration;
using Serilog;
using Constants = MD.Home.Server.Others.Constants;

namespace MD.Home.Server
{
    public class MangaDexClient : IDisposable
    {
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
        private readonly ClientSettings _clientSettings;
        private readonly JsonSerializerOptions _serializerOptions;
        
        private readonly Timer _pingTimer;

        private bool _isLoggedIn;
        private RemoteSettings? _remoteSettings;

        public MangaDexClient(ILogger logger, ClientSettings clientSettings, JsonSerializerOptions serializerOptions)
        {
            _logger = logger;
            _clientSettings = clientSettings;
            _serializerOptions = serializerOptions;

            _pingTimer = new Timer(PingControl, null, TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(45));
        }

        public async Task LoginToControl()
        {
            _logger.Information("Connecting to the control server");

            var message = JsonSerializer.Serialize(GetPingParameters(), _serializerOptions);
            var response = await Program.HttpClient.PostAsync($"{Constants.ServerAddress}ping", new StringContent(message, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                _remoteSettings = JsonSerializer.Deserialize<RemoteSettings>(await response.Content.ReadAsStringAsync(), _serializerOptions);
                _isLoggedIn = true;
                
                if (_remoteSettings?.LatestBuild > Constants.ClientBuild)
                    _logger.Warning($"Outdated build detected! Latest: {_remoteSettings.LatestBuild}, Current: {Constants.ClientBuild}");
            }
            else
                throw new AuthenticationException();
        }

        public async Task LogoutFromControl()
        {
            _logger.Information("Disconnecting from the control server");

            var message = JsonSerializer.Serialize(new Dictionary<string, object> { {"secret", _clientSettings.ClientSecret} }, _serializerOptions);
            var response = await Program.HttpClient.PostAsync($"{Constants.ServerAddress}stop", new StringContent(message, Encoding.UTF8, "application/json"));

            _isLoggedIn = false;
            
            if (!response.IsSuccessStatusCode)
                throw new AuthenticationException();
        }
        
        public void Dispose()
        {
            lock (this)
            {
                if (!_isLoggedIn)
                    throw new ObjectDisposedException($"This instance of {nameof(MangaDexClient)} has been disposed.");

                _isLoggedIn = false;
            }

            GC.SuppressFinalize(this);
            _pingTimer.Dispose();
        }

        private async void PingControl(object? state)
        {
            if (_remoteSettings == null || !_isLoggedIn)
                return;

            _logger.Information("Pinging the control server");
            
            var message = JsonSerializer.Serialize(GetPingParameters(), _serializerOptions);
            HttpResponseMessage response;

            try
            {
                response = await Program.HttpClient.PostAsync($"{Constants.ServerAddress}ping", new StringContent(message, Encoding.UTF8, "application/json"));
            }
            catch
            {
                _logger.Error("Failed to ping control due to unknown reasons");

                return;
            }

            if (response.IsSuccessStatusCode)
            {
                var remoteSettings = JsonSerializer.Deserialize<RemoteSettings>(await response.Content.ReadAsStringAsync(), _serializerOptions);
                
                _logger.Information($"Server settings received: {remoteSettings}");

                if (remoteSettings?.LatestBuild > Constants.ClientBuild)
                    _logger.Warning($"Outdated build detected! Latest: {remoteSettings.LatestBuild}, Current: {Constants.ClientBuild}");

                if (!ValidateSettings(remoteSettings))
                {
                    _logger.Information("Restarting ImageServer to refresh certificates");
                    _remoteSettings = remoteSettings;
                    
                    Program.Restart();
                }
                else
                    _remoteSettings = remoteSettings;
            }
            else
                _logger.Information("Server ping failed - ignoring");
        }

        private Dictionary<string, object> GetPingParameters()
        {
            var message = new Dictionary<string, object>
            {
                {"secret", _clientSettings.ClientSecret},
                {"port", _clientSettings.ClientExternalPort != 0 ? _clientSettings.ClientExternalPort : _clientSettings.ClientPort},
                {"disk_space", _clientSettings.MaxCacheSizeInMebibytes * 1024 * 1024},
                {"network_speed", 0},
                {"build_version", Constants.ClientBuild}
            };

            if (_remoteSettings?.TlsCertificate != null)
                message.Add("tls_created_at", _remoteSettings.TlsCertificate.CreatedAt.ToString("O"));

            return message;
        }
        
        private bool ValidateSettings(RemoteSettings? remoteSettings) =>
            RemoteSettings.TlsCertificate?.Certificate == remoteSettings?.TlsCertificate?.Certificate &&
            RemoteSettings.TlsCertificate?.Certificate == remoteSettings?.TlsCertificate?.Certificate;
    }
}