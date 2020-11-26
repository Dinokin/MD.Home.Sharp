using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentQueue<CacheEntry> _insertionQueue = new();

        private bool _isDisposed;

        public CacheManager(ClientSettings clientSettings)
        {
            _maxCacheSize = Convert.ToUInt64(clientSettings.MaxCacheSizeInMebibytes * 1024 * 1024);

            _cacheEntryDao = new CacheEntryDao(Constants.CacheFile, 100);
            _memoryCache = new MemoryCache(new MemoryCacheOptions {SizeLimit = clientSettings.MaxPagesInMemory});

            _insertionTimer = new Timer(InsertionTasks, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public CacheEntry? GetEntry(Guid id)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");
            
            var entry = _memoryCache.Get<CacheEntry?>(id);

            if (entry != null)
            {
                entry.LastAccessed = DateTime.UtcNow;

                return entry;
            }
            
            entry = _cacheEntryDao.GetEntryById(id);

            if (entry == null)
                return null;
            
            InsertIntoMemory(entry);

            return entry;
        }

        public void InsertEntry(CacheEntry cacheEntry)
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
            
            ConsolidateDatabase();
            
            _cacheEntryDao.Dispose();
        }

        private void InsertIntoMemory(CacheEntry entry)
        {
            entry.LastAccessed = DateTime.UtcNow;
            
            var entryOptions = new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromHours(1),
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
            
            _memoryCache.Set(entry.Id, entry, entryOptions);
        }

        private void ConsolidateDatabase()
        {
            if (_cacheEntryDao.TotalSizeOfContents is var totalSizeOfContents && totalSizeOfContents > _maxCacheSize)
                ReduceCacheBySize(Convert.ToUInt64(Math.Ceiling(_maxCacheSize / 100d)));

            _cacheEntryDao.TriggerCheckpoint();
        }
        
        private void ReduceCacheBySize(ulong size)
        {
            var averageSize = _cacheEntryDao.AverageSizeOfContents;

            _cacheEntryDao.DeleteLeastAccessedEntries(Convert.ToUInt32(Math.Ceiling(size / averageSize)));
        }

        private void InsertionTasks(object? state)
        {
            try
            {
                while (_insertionQueue.TryDequeue(out var entry))
                    _cacheEntryDao.InsertEntry(entry);

                ConsolidateDatabase();
            }
            catch
            {
                // Ignore
            }
        }
    }
}