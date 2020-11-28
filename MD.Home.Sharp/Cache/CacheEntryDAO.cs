using System;
using System.Data;
using MD.Home.Sharp.Extensions;
using Microsoft.Data.Sqlite;

namespace MD.Home.Sharp.Cache
{
    internal class CacheEntryDao : IDisposable
    {
        public ulong AmountOfCacheEntries
        {
            get
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
                
                using var command = new SqliteCommand(Queries.AmountOfCacheEntries);
                using var reader = ExecuteQueryWithResult(command, CommandBehavior.SingleResult);

                return reader.Read() ? Convert.ToUInt64(reader.GetInt64(0)) : 0;
            }
        }

        private readonly ConnectionPool _connectionPool;

        private bool _isDisposed;

        public CacheEntryDao(string dataSource)
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            _connectionPool = new ConnectionPool(connectionStringBuilder.ToString());
            CreateDatabase();
        }

        public CacheEntry? GetCacheEntryById(Guid cacheEntryId)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
            
            using var command = new SqliteCommand(Queries.GetCacheEntryById);
            command.Parameters.Add(new SqliteParameter("$id", cacheEntryId.ToString("N")));

            using var reader = ExecuteQueryWithResult(command, CommandBehavior.SingleResult);

            CacheEntry? cacheEntry = null;

            if (reader.Read())
                cacheEntry = new CacheEntry
                {
                    Id = reader.GetGuid("id"),
                    ContentType = reader.GetString("content_type"),
                    LastModified = reader.GetDateTime("last_modified"),
                    Content = reader.GetStream("content").GetBytes()
                };
            
            return cacheEntry;
        }

        public void InsertCacheEntry(CacheEntry cacheEntry)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
            
            using var command = new SqliteCommand(Queries.InsertCacheEntry);
            
            command.Parameters.AddRange(new []
            {
                new SqliteParameter("$id", cacheEntry.Id.ToString("N")),
                new SqliteParameter("$content_type", cacheEntry.ContentType),
                new SqliteParameter("$last_accessed", cacheEntry.LastAccessed),
                new SqliteParameter("$last_modified", cacheEntry.LastModified),
                new SqliteParameter("$content", cacheEntry.Content)
            });
            
            ExecuteQueryWithoutResult(command);
        }

        public void UpdateEntryLastAccessDate(Guid id, DateTime date)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
            
            using var command = new SqliteCommand(Queries.UpdateCacheEntryLastAccessDate);
            
            command.Parameters.AddRange(new []
            {
                new SqliteParameter("$id", id.ToString("N")),
                new SqliteParameter("$last_accessed", date),
            });
            
            ExecuteQueryWithoutResult(command);
        }

        public void DeleteLeastAccessedEntries(ulong amount)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");

            using var command = new SqliteCommand(Queries.DeleteLeastAccessedCacheEntries);

            command.Parameters.Add(new SqliteParameter("$amount", amount));

            ExecuteQueryWithoutResult(command);
        }
        
        public void TriggerCheckpoint()
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
            
            using var command = new SqliteCommand(Queries.TriggerCheckpoint);
            
            ExecuteQueryWithoutResult(command);
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
                
                _isDisposed = true;
            }
            
            GC.SuppressFinalize(this);
            
            _connectionPool.Dispose();
        }
        
        private void CreateDatabase()
        {
            try
            {
                using var command1 = new SqliteCommand(Queries.SetJournalMode);
                using var command2 = new SqliteCommand(Queries.DisableAutocheckpoint);
                using var command3 = new SqliteCommand(Queries.SetCacheSize);
                using var command4 = new SqliteCommand(Queries.CreateDatabase);

                ExecuteQueryWithoutResult(command1);
                ExecuteQueryWithoutResult(command2);
                ExecuteQueryWithoutResult(command3);
                ExecuteQueryWithoutResult(command4);
            }
            catch
            {
                // Ignore
            }
        }

        private void ExecuteQueryWithoutResult(SqliteCommand command)
        {
            PrepareCommand(command);

            command.ExecuteNonQuery();
        }

        private SqliteDataReader ExecuteQueryWithResult(SqliteCommand command, CommandBehavior commandBehavior)
        {
            PrepareCommand(command);

            return command.ExecuteReader(commandBehavior);
        }

        private void PrepareCommand(SqliteCommand command)
        {
            command.Connection = _connectionPool.GetConnection();
            command.Disposed += (sender, _) => CommandDisposer(sender as SqliteCommand);
        }

        private void CommandDisposer(SqliteCommand? command)
        {
            if (command == null)
                return;
            
            _connectionPool.ReturnConnection(command.Connection);
        }
    }
}