using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using MD.Home.Server.Configuration.Types;
using MD.Home.Server.Extensions;

namespace MD.Home.Server.Configuration
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public sealed class RemoteSettings
    {
        public string Url { get; }
        public string ImageServer { get; }
        public string TokenKey { get; }
        public byte[] DecodedToken { get; }
        [JsonPropertyName("compromised")]
        public bool IsCompromised { get; }
        [JsonPropertyName("paused")]
        public bool IsPaused { get; }
        public bool ForceTokens { get; }
        public ushort LatestBuild { get; }
        [JsonPropertyName("tls")]
        public TlsCertificate? TlsCertificate { get; }
        
        public RemoteSettings(string url, string imageServer, string tokenKey, bool isCompromised, bool isPaused, bool forceTokens, ushort latestBuild, TlsCertificate? tlsCertificate)
        {
            Url = url;
            ImageServer = imageServer;
            TokenKey = tokenKey;
            IsCompromised = isCompromised;
            IsPaused = isPaused;
            ForceTokens = forceTokens;
            LatestBuild = latestBuild;
            TlsCertificate = tlsCertificate;

            DecodedToken = tokenKey.DecodeToken();
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