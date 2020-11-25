using System;
using System.Data;
using MD.Home.Server.Extensions;
using Microsoft.Data.Sqlite;

namespace MD.Home.Server.Cache
{
    public class CacheEntryDao : IDisposable
    {
        public double AverageSizeOfContents => GetAverageSizeOfContents();
        public ulong TotalSizeOfContents => GetTotalSizeOfContents();

        private readonly ConnectionPool _connectionPool;

        private bool _isDisposed;

        public CacheEntryDao(string fileName, ushort connectionPoolSize)
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = fileName,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            _connectionPool = new ConnectionPool(connectionStringBuilder.ToString(), connectionPoolSize);
            CreateDatabase();
        }

        public CacheEntry? GetEntryById(Guid entryId)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
            
            using var command = new SqliteCommand(Queries.GetEntryById);
            command.Parameters.Add(new SqliteParameter("$id", entryId.ToString("N")));

            using var reader = ExecuteQueryWithResult(command, CommandBehavior.SingleResult);

            CacheEntry? cacheEntry = null;

            if (reader.Read())
                cacheEntry = new CacheEntry
                {
                    Id = reader.GetGuid("id"),
                    ContentType = reader.GetString("content_type"),
                    LastAccessed = reader.GetDateTime("last_accessed"),
                    LastModified = reader.GetDateTime("last_modified"),
                    Size = Convert.ToUInt64(reader.GetInt64("size")),
                    Content = reader.GetStream("content").GetBytes()
                };
            
            return cacheEntry;
        }

        public void InsertEntry(CacheEntry cacheEntry)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
            
            using var command = new SqliteCommand(Queries.InsertEntry);
            
            command.Parameters.AddRange(new []
            {
                new SqliteParameter("$id", cacheEntry.Id.ToString("N")),
                new SqliteParameter("$content_type", cacheEntry.ContentType),
                new SqliteParameter("$last_accessed", cacheEntry.LastAccessed),
                new SqliteParameter("$last_modified", cacheEntry.LastModified),
                new SqliteParameter("$size", cacheEntry.Size),
                new SqliteParameter("$content", cacheEntry.Content)
            });
            
            ExecuteQueryWithoutResult(command);
        }

        public void UpdateEntryLastAccessDate(Guid id, DateTime date)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
            
            using var command = new SqliteCommand(Queries.UpdateEntryLastAccessDate);
            
            command.Parameters.AddRange(new []
            {
                new SqliteParameter("$id", id.ToString("N")),
                new SqliteParameter("$last_accessed", date),
            });
            
            ExecuteQueryWithoutResult(command);
        }

        public void DeleteLeastAccessedEntries(uint amount)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");

            using var command = new SqliteCommand(Queries.DeleteLeastAccessedEntries);

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

        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
                
                _isDisposed = true;
            }
            
            _connectionPool.Dispose();
            GC.SuppressFinalize(this);
        }

        private double GetAverageSizeOfContents()
        {
            try
            {
                using var command = new SqliteCommand(Queries.AverageSizeOfContents);
                using var reader = ExecuteQueryWithResult(command, CommandBehavior.SingleResult);

                return reader.Read() ? reader.GetDouble(0) : 0;
            }
            catch
            {
                return 0;
            }
        }
        
        private ulong GetTotalSizeOfContents()
        {
            try
            {
                using var command = new SqliteCommand(Queries.TotalSizeOfContents);
                using var reader = ExecuteQueryWithResult(command, CommandBehavior.SingleResult);

                return reader.Read() ? Convert.ToUInt64(reader.GetInt64(0)) : 0;
            }
            catch
            {
                return 0;
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

        private static void CommandDisposer(SqliteCommand? command) => command?.Connection?.Close();
    }
}