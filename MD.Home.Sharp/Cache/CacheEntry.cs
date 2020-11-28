using System;

namespace MD.Home.Sharp.Cache
{
    public sealed class CacheEntry
    {
        public Guid Id { get; init; }
        public string ContentType { get; init; } = string.Empty;
        public DateTime LastModified { get; init; }
        public DateTime LastAccessed { get; set; }
        public byte[] Content { get; init; } = Array.Empty<byte>();
    }
}