﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MD.Home.Sharp.Configuration;
using Serilog;
using Constants = MD.Home.Sharp.Others.Constants;

namespace MD.Home.Sharp
{
    internal class MangaDexClient : IDisposable
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

        private readonly ClientSettings _clientSettings;
        private readonly JsonSerializerOptions _serializerOptions;
        
        private readonly Timer _pingTimer;

        private bool _isLoggedIn;
        private RemoteSettings? _remoteSettings;

        private bool _isDisposed;

        public MangaDexClient(ClientSettings clientSettings, JsonSerializerOptions serializerOptions)
        {
            _clientSettings = clientSettings;
            _serializerOptions = serializerOptions;

            _pingTimer = new Timer(PingControl, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
        }

        public async Task LoginToControl()
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(MangaDexClient)} has been disposed.");
            
            Log.Logger.Information("Connecting to the control server");

            var message = JsonSerializer.Serialize(GetPingParameters(), _serializerOptions);
            var response = await Program.HttpClient.PostAsync($"{Constants.ServerAddress}ping", new StringContent(message, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                _remoteSettings = JsonSerializer.Deserialize<RemoteSettings>(await response.Content.ReadAsStringAsync(), _serializerOptions);
                _isLoggedIn = true;
                
                if (_remoteSettings?.LatestBuild > Constants.ClientBuild)
                    Log.Logger.Warning($"Outdated build detected! Latest: {_remoteSettings.LatestBuild}, Current: {Constants.ClientBuild}");
            }
            else
                throw new AuthenticationException();
        }

        public async Task LogoutFromControl()
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(MangaDexClient)} has been disposed.");
            
            Log.Logger.Information("Disconnecting from the control server");

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
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(MangaDexClient)} has been disposed.");

                _isLoggedIn = false;
                _isDisposed = true;
            }

            _pingTimer.Dispose();
            GC.SuppressFinalize(this);
        }

        private async void PingControl(object? state)
        {
            if (_remoteSettings == null || !_isLoggedIn || _isDisposed)
                return;

            Log.Logger.Debug("Pinging the control server");
            
            var message = JsonSerializer.Serialize(GetPingParameters(), _serializerOptions);
            HttpResponseMessage response;

            try
            {
                response = await Program.HttpClient.PostAsync($"{Constants.ServerAddress}ping", new StringContent(message, Encoding.UTF8, "application/json"));
            }
            catch
            {
                Log.Logger.Error("Failed to ping control due to unknown reasons");

                return;
            }

            if (response.IsSuccessStatusCode)
            {
                var remoteSettings = JsonSerializer.Deserialize<RemoteSettings>(await response.Content.ReadAsStringAsync(), _serializerOptions);
                
                Log.Logger.Information($"Server settings received: {remoteSettings}");

                if (remoteSettings?.LatestBuild > Constants.ClientBuild)
                    Log.Logger.Warning($"Outdated build detected! Latest: {remoteSettings.LatestBuild}, Current: {Constants.ClientBuild}");

                if (remoteSettings?.TlsCertificate != null)
                {
                    Log.Logger.Warning("Restarting ImageServer to refresh certificates");
                    _remoteSettings = remoteSettings;
                    
                    Program.Restart();
                }
                else if (remoteSettings != null)
                {
                    remoteSettings.TlsCertificate = _remoteSettings.TlsCertificate;
                    _remoteSettings = remoteSettings;
                }
            }
            else
                Log.Logger.Error("Ping to control failed");
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
                message.Add("tls_created_at", _remoteSettings.TlsCertificate.CreatedAt);

            return message;
        }
    }
}