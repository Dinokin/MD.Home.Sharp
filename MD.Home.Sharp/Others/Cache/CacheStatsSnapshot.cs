using System;

namespace MD.Home.Sharp.Others.Cache
{
    public sealed record CacheStatsSnapshot
    {
        public DateTimeOffset StartTime { get; init; }
        public DateTimeOffset SnapshotTime { get; init; }
        
        public ulong HitCount { get; init; }
        public ulong MissCount { get; init; }
        
        public double AverageHitTtfb { get; init; }
        public double AverageMissTtfb { get; init; }
    }
}