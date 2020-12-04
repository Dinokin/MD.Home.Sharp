using System;

namespace MD.Home.Sharp.Cache
{
    public sealed record CacheEntry
    {
        public string Hash { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public DateTimeOffset LastModified { get; init; }
        public DateTimeOffset LastAccessed { get; set; }
        public byte[] Content { get; init; } = Array.Empty<byte>();
    }
}