using System;
using System.Runtime.CompilerServices;

namespace MD.Home.Sharp.Others.Cache
{
    public sealed class CacheStats
    {
        public CacheStatsSnapshot Snapshot
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => new()
            {
                StartTime = StartTime,
                SnapshotTime = DateTimeOffset.UtcNow,
                HitCount = _hitCount,
                MissCount = _missCount,
                AverageHitTtfb = _accumulatedHitTtfb.TotalMilliseconds / _hitCount,
                AverageMissTtfb = _accumulatedMissTtfb.TotalMilliseconds / _hitCount
            };
        }
        
        private DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

        private ulong _hitCount;
        private ulong _missCount;

        private TimeSpan _accumulatedHitTtfb = TimeSpan.Zero;
        private TimeSpan _accumulatedMissTtfb = TimeSpan.Zero;

        private readonly object _hitLock = new();
        private readonly object _missLock = new();

        public void IncrementHit(TimeSpan ttfb)
        {
            lock (_hitLock)
            {
                _accumulatedHitTtfb += ttfb;
                _hitCount++;
            }
        }

        public void IncrementMiss(TimeSpan ttfb)
        {
            lock (_missLock)
            {
                _accumulatedMissTtfb += ttfb;
                _missCount++;
            }
        }
    }
}