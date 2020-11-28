using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using MD.Home.Sharp.Configuration;
using MD.Home.Sharp.Others;
using Microsoft.Extensions.Caching.Memory;

namespace MD.Home.Sharp.Cache
{
    public class CacheManager : IDisposable
    {
        private readonly ulong _maxCacheSize;

        private readonly CacheEntryDao _cacheEntryDao;
        private readonly MemoryCache _memoryCache;

        private readonly Timer _insertionTimer;
        private readonly ConcurrentQueue<CacheEntry> _insertionQueue;
        private readonly FileInfo _cacheFile;

        private bool _isDisposed;

        public CacheManager(ClientSettings clientSettings)
        {
            _maxCacheSize = Convert.ToUInt64(clientSettings.MaxCacheSizeInMebibytes * 1024 * 1024);
            _insertionQueue = new ConcurrentQueue<CacheEntry>();
            _cacheEntryDao = new CacheEntryDao(Constants.CacheFile);
            _memoryCache = new MemoryCache(new MemoryCacheOptions {SizeLimit = clientSettings.MaxPagesInMemory});
            _cacheFile = new FileInfo(Constants.CacheFile);

            _insertionTimer = new Timer(InsertionTasks, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public CacheEntry? GetCacheEntry(Guid cacheEntryId)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");
            
            var cacheEntry = _memoryCache.Get<CacheEntry?>(cacheEntryId);

            if (cacheEntry != null)
            {
                cacheEntry.LastAccessed = DateTime.UtcNow;

                return cacheEntry;
            }
            
            cacheEntry = _cacheEntryDao.GetCacheEntryById(cacheEntryId);

            if (cacheEntry == null)
                return null;
            
            InsertIntoMemory(cacheEntry);

            return cacheEntry;
        }

        public void InsertCacheEntry(CacheEntry cacheEntry)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");
            
            InsertIntoMemory(cacheEntry);
            _insertionQueue.Enqueue(cacheEntry);
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");

                _isDisposed = true;
            }
            
            GC.SuppressFinalize(this);
            
            _insertionTimer.Dispose();
            
            _memoryCache.Compact(100);
            _memoryCache.Dispose();

            ConsolidateDatabase();
            
            _cacheEntryDao.Dispose();
        }

        private void InsertIntoMemory(CacheEntry cacheEntry)
        {
            cacheEntry.LastAccessed = DateTime.UtcNow;
            
            var entryOptions = new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(10),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                PostEvictionCallbacks = { new PostEvictionCallbackRegistration() }
            };

            entryOptions.PostEvictionCallbacks.First().EvictionCallback += (key, value, _, _) =>
            {
                try
                {
                    _cacheEntryDao.UpdateEntryLastAccessDate((Guid) key, ((CacheEntry) value).LastAccessed);
                }
                catch
                {
                    // Ignore
                }
            };
            
            _memoryCache.Set(cacheEntry.Id, cacheEntry, entryOptions);
        }

        private void ConsolidateDatabase()
        {
            if (Convert.ToUInt64(_cacheFile.Length) > _maxCacheSize + _maxCacheSize / 100)
                ReduceCacheBySize(Convert.ToUInt64(Math.Ceiling(_maxCacheSize / 100d * 5)));

            _cacheEntryDao.TriggerCheckpoint();
        }
        
        private void ReduceCacheBySize(ulong size)
        {
            var averageSize = Convert.ToUInt64(_cacheFile.Length) / _cacheEntryDao.AmountOfCacheEntries;

            _cacheEntryDao.DeleteLeastAccessedEntries(Convert.ToUInt64(Math.Ceiling(Convert.ToDouble(size / averageSize))));
        }

        private void InsertionTasks(object? state)
        {
            try
            {
                while (_insertionQueue.TryDequeue(out var entry))
                    _cacheEntryDao.InsertCacheEntry(entry);

                ConsolidateDatabase();
            }
            catch
            {
                // Ignore
            }
        }
    }
}