using System.Diagnostics.CodeAnalysis;

namespace MD.Home.Server.Configuration
{
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
    public sealed class ClientSettings
    {
        public string ClientHostname { get; set; } = "0.0.0.0";
        public ushort ClientPort { get; set; } = 443;
        public ushort ClientExternalPort { get; set; } = 443;
        public string ClientSecret { get; set; } = string.Empty;
        public ulong MaxCacheSizeInMebibytes { get; set; } = 1024;
        public uint MaxEntriesInMemory { get; set; } = 20;
    }
}