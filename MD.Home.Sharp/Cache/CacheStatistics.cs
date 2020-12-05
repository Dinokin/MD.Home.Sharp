using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MD.Home.Sharp.Cache
{
    public static class CacheStatistics
    {
        public static DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

        public static ulong HitCount => Interlocked.Read(ref _hitCount);
        public static ulong MissCount => Interlocked.Read(ref _missCount);

        public static double AverageHitTtfb
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                if (_hitCount == 0)
                    return 0;
                
                return _accumulatedHitTtfb.TotalMilliseconds / _hitCount;
            }
        }

        public static double AverageMissTtfb
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                if (_missCount == 0)
                    return 0;
                
                return _accumulatedMissTtfb.TotalMilliseconds / _missCount;
            }
        }

        private static ulong _hitCount;
        private static ulong _missCount;

        private static TimeSpan _accumulatedHitTtfb = new();
        private static TimeSpan _accumulatedMissTtfb = new();

        private static readonly object HitLock = new();
        private static readonly object MissLock = new();

        public static void IncrementHit() => Interlocked.Increment(ref _hitCount);
        public static void IncrementMiss() => Interlocked.Increment(ref _missCount);

        public static void IncrementHitTtfb(TimeSpan ttfb)
        {
            lock (HitLock)
                _accumulatedHitTtfb += ttfb;
        }

        public static void IncrementMissTtfb(TimeSpan ttfb)
        {
            lock (MissLock)
                _accumulatedMissTtfb += ttfb;
        }
    }
}