using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using MD.Home.Server.Others;
using Microsoft.Extensions.Caching.Memory;

namespace MD.Home.Server.Cache
{
    public class CacheManager : IDisposable
    {
        private readonly CacheEntryDao _cacheEntryDao;
        private readonly MemoryCache _memoryCache;
        private readonly ulong _maxCacheSize;
        private readonly ConcurrentQueue<CacheEntry> _insertQueue = new();

        private bool _isDisposed;

        public CacheManager(MangaDexClient mangaDexClient)
        {
            _cacheEntryDao = new CacheEntryDao(Constants.CacheFile, 100);
            _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = mangaDexClient.ClientSettings.MaxEntriesInMemory });
            _maxCacheSize = Convert.ToUInt64(mangaDexClient.ClientSettings.MaxCacheSizeInMebibytes * 1024 * 1024);

            var writer = new Thread(() =>
            {
                var count = 0;
                
                while (!_isDisposed)
                {
                    try
                    {
                        if (_insertQueue.TryDequeue(out var entry))
                        {
                            _cacheEntryDao.InsertEntry(entry);

                            count++;
                        }
                        else
                            Thread.Sleep(TimeSpan.FromSeconds(1));

                        if (count <= 50)
                            continue;

                        TrimDatabase();
                        count = 0;
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            });
            
            writer.Start();
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
            
            var entryOptions = new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromHours(1),
                PostEvictionCallbacks = { new PostEvictionCallbackRegistration() }
            };

            entryOptions.PostEvictionCallbacks.First().EvictionCallback += (key, value, _, _) => _cacheEntryDao.UpdateEntryLastAccessDate((Guid) key, ((CacheEntry) value).LastAccessed);
            _memoryCache.Set(id, entry, entryOptions);

            return entry;
        }

        public void InsertEntry(CacheEntry cacheEntry)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");
            
            _insertQueue.Enqueue(cacheEntry);
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");

                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
            
            _memoryCache.Compact(100);
            _memoryCache.Dispose();
            TrimDatabase();
            _cacheEntryDao.Dispose();
        }

        private void TrimDatabase()
        {
            if (_cacheEntryDao.TotalSizeOfContents is var totalSizeOfContents && totalSizeOfContents > _maxCacheSize)
                ReduceCacheSize(totalSizeOfContents - _maxCacheSize);

            _cacheEntryDao.TriggerCheckpoint();
        }
        
        private void ReduceCacheSize(ulong size)
        {
            var averageSize = _cacheEntryDao.AverageSizeOfContents;

            _cacheEntryDao.DeleteLeastAccessedEntries(averageSize >= size ? 1 : Convert.ToUInt32(Math.Ceiling(size / averageSize) * 2));
        }
    }
}