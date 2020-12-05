using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using MD.Home.Sharp.Configuration;
using MD.Home.Sharp.Extensions;
using MD.Home.Sharp.Others;
using Microsoft.Extensions.Caching.Memory;

namespace MD.Home.Sharp.Cache
{
    public class CacheManager : IDisposable
    {
        private readonly ulong _maxCacheSize;
        private readonly FileInfo _cacheFile;
        private readonly CacheEntryDao _cacheEntryDao;
        private readonly MemoryCache _memoryCache;

        private readonly ConcurrentQueue<CacheEntry> _insertionQueue;
        private readonly Timer _insertionTimer;

        private bool _isDisposed;

        public CacheManager(ClientSettings clientSettings)
        {
            _maxCacheSize = Convert.ToUInt64(clientSettings.MaxCacheSizeInMebibytes * 1024 * 1024);
            _cacheFile = new FileInfo(Constants.CacheFile);
            _cacheEntryDao = new CacheEntryDao(_cacheFile);
            _memoryCache = new MemoryCache(new MemoryCacheOptions {SizeLimit = clientSettings.MaxPagesInMemory});
            
            _insertionQueue = new ConcurrentQueue<CacheEntry>();
            _insertionTimer = new Timer(InsertionTasks, "Timer", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public CacheEntry? GetCacheEntry(string url)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");

            var hash = url.GetMd5Hash();
            var cacheEntry = _memoryCache.Get<CacheEntry?>(hash);

            if (cacheEntry != null)
            {
                cacheEntry.LastAccessed = DateTimeOffset.UtcNow;

                return cacheEntry;
            }
            
            cacheEntry = _cacheEntryDao.GetCacheEntryByHash(hash);

            if (cacheEntry == null)
                return null;
            
            InsertIntoMemory(cacheEntry);

            return cacheEntry;
        }

        public CacheEntry InsertCacheEntry(string url, string contentType, DateTimeOffset lastModified, byte[] content)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");

            var entry = new CacheEntry
            {
                Hash = url.GetMd5Hash(),
                ContentType = contentType,
                LastModified = lastModified,
                LastAccessed = DateTimeOffset.UtcNow,
                Content = content
            };
            
            InsertIntoMemory(entry);
            _insertionQueue.Enqueue(entry);

            return entry;
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");

                _isDisposed = true;
            }
            
            _insertionTimer.Dispose();
            
            _memoryCache.Compact(100);
            _memoryCache.Dispose();
            
            InsertionTasks("Disposing");

            _cacheEntryDao.Dispose();
            GC.SuppressFinalize(this);
        }

        private void InsertIntoMemory(CacheEntry cacheEntry)
        {
            cacheEntry.LastAccessed = DateTimeOffset.UtcNow;

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
                    _cacheEntryDao.UpdateEntryLastAccessDate((string) key, ((CacheEntry) value).LastAccessed);
                }
                catch
                {
                    // Ignore
                }
            };
            
            _memoryCache.Set(cacheEntry.Hash, cacheEntry, entryOptions);
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
            if (_isDisposed && (string?) state == "Timer")
                return;

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