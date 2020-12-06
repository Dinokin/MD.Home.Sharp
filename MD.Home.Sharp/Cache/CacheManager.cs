using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using MD.Home.Sharp.Configuration;
using MD.Home.Sharp.Extensions;
using MD.Home.Sharp.Others;

namespace MD.Home.Sharp.Cache
{
    public class CacheManager : IDisposable
    {
        private enum CommandType
        {
            Insert,
            Update
        }

        private readonly ulong _maxCacheSize;
        private readonly FileInfo _cacheFile;
        private readonly CacheEntryDao _cacheEntryDao;

        private readonly ConcurrentQueue<(CommandType Type, CacheEntry CacheEntry)> _commandQueue;
        private readonly Timer _queueTimer;

        private bool _isDisposed;

        public CacheManager(ClientSettings clientSettings)
        {
            _maxCacheSize = Convert.ToUInt64(clientSettings.MaxCacheSizeInMebibytes * 1024 * 1024);
            _cacheFile = new FileInfo(Constants.CacheFile);
            _cacheEntryDao = new CacheEntryDao(_cacheFile);
            
            _commandQueue = new ConcurrentQueue<(CommandType, CacheEntry)>();
            _queueTimer = new Timer(ExecuteCommands, "Timer", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public CacheEntry? GetCacheEntry(string url)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");

            var cacheEntry = _cacheEntryDao.GetCacheEntryByHash(url.GetMd5Hash());

            if (cacheEntry == null)
                return null;
            
            cacheEntry.LastAccessed = DateTimeOffset.UtcNow;
            _commandQueue.Enqueue((CommandType.Update, cacheEntry));
            
            return cacheEntry;
        }

        public CacheEntry InsertCacheEntry(string url, string contentType, DateTimeOffset lastModified, byte[] content)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");

            var cacheEntry = new CacheEntry
            {
                Hash = url.GetMd5Hash(),
                ContentType = contentType,
                LastModified = lastModified,
                LastAccessed = DateTimeOffset.UtcNow,
                Content = content
            };
            
            _commandQueue.Enqueue((CommandType.Insert, cacheEntry));

            return cacheEntry;
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(CacheManager)} has been disposed.");

                _isDisposed = true;
            }
            
            _queueTimer.Dispose();

            ExecuteCommands("Disposing");

            _cacheEntryDao.Dispose();
            GC.SuppressFinalize(this);
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

        private void ExecuteCommands(object? state)
        {
            if (_isDisposed && (string?) state == "Timer")
                return;

            try
            {
                while (_commandQueue.TryDequeue(out var command))
                    switch (command.Type)
                    {
                        case CommandType.Insert:
                            _cacheEntryDao.InsertCacheEntry(command.CacheEntry);
                            break;
                        case CommandType.Update:
                            _cacheEntryDao.UpdateEntryLastAccessDate(command.CacheEntry.Hash, command.CacheEntry.LastAccessed);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Command {command.Type} is not supported.");
                    }

                ConsolidateDatabase();
            }
            catch
            {
                // Ignore
            }
        }
    }
}