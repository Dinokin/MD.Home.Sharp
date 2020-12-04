﻿using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using MD.Home.Sharp.Configuration.Types;
using MD.Home.Sharp.Serialization;

namespace MD.Home.Sharp.Configuration
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    internal sealed class RemoteSettings
    {
        public string Url { get; }
        
        public string ImageServer { get; }
        
        [JsonConverter(typeof(Base64StringDecoder))]
        public byte[] TokenKey { get; }
        
        [JsonPropertyName("compromised")]
        public bool IsCompromised { get; }
        
        [JsonPropertyName("paused")]
        public bool IsPaused { get; }
        
        public bool ForceTokens { get; }
        
        public ushort LatestBuild { get; }
        
        [JsonPropertyName("tls")]
        public TlsCertificate? TlsCertificate { get; set; }

        public RemoteSettings(string url, string imageServer, byte[] tokenKey, bool isCompromised, bool isPaused, bool forceTokens, ushort latestBuild, TlsCertificate? tlsCertificate)
        {
            Url = url;
            ImageServer = imageServer;
            TokenKey = tokenKey;
            IsCompromised = isCompromised;
            IsPaused = isPaused;
            ForceTokens = forceTokens;
            LatestBuild = latestBuild;
            TlsCertificate = tlsCertificate;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append($"{nameof(Url)}: {Url}, ");
            sb.Append($"{nameof(ImageServer)}: {ImageServer}, ");
            sb.Append($"{nameof(IsCompromised)}: {IsCompromised}, ");
            sb.Append($"{nameof(IsPaused)}: {IsPaused}, ");
            sb.Append($"{nameof(LatestBuild)}: {LatestBuild}");

            return sb.ToString();
        }
    }
}