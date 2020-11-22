using System;
using System.Data;
using MD.Home.Server.Extensions;
using MD.Home.Server.Serialization;
using Microsoft.Data.Sqlite;

namespace MD.Home.Server.Cache
{
    public class CacheEntryDao : IDisposable
    {
        public double AverageSizeOfContents => GetAverageSizeOfContents();
        public ulong TotalSizeOfContents => GetTotalSizeOfContents();

        private readonly ConnectionPool _connectionPool;
        private readonly SnakeCaseNamingPolicy _snakeConv = new();

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
            command.Parameters.Add(new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.Id))}", entryId.ToString("N")));

            using var reader = ExecuteQueryWithResult(command, CommandBehavior.SingleResult);

            CacheEntry? cacheEntry = null;

            if (reader.Read())
                cacheEntry = new CacheEntry
                {
                    Id = reader.GetGuid(_snakeConv.ConvertName(nameof(CacheEntry.Id))),
                    ContentType = reader.GetString(_snakeConv.ConvertName(nameof(CacheEntry.ContentType))),
                    LastAccessed = reader.GetDateTime(_snakeConv.ConvertName(nameof(CacheEntry.LastAccessed))),
                    LastModified = reader.GetDateTime(_snakeConv.ConvertName(nameof(CacheEntry.LastModified))),
                    Size = Convert.ToUInt64(reader.GetInt64(_snakeConv.ConvertName(nameof(CacheEntry.Size)))),
                    Content = reader.GetStream(_snakeConv.ConvertName(nameof(CacheEntry.Content))).GetBytes()
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
                new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.Id))}", cacheEntry.Id.ToString("N")),
                new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.ContentType))}", cacheEntry.ContentType),
                new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.LastModified))}", cacheEntry.LastModified),
                new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.LastAccessed))}", cacheEntry.LastAccessed),
                new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.Size))}", cacheEntry.Size),
                new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.Content))}", cacheEntry.Content)
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
                new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.Id))}", id.ToString("N")),
                new SqliteParameter($"${_snakeConv.ConvertName(nameof(CacheEntry.LastAccessed))}", date),
            });
            
            ExecuteQueryWithoutResult(command);
        }

        public void DeleteLeastAccessedEntries(uint amount)
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");

            using var command = new SqliteCommand(Queries.DeleteLeastAccessedEntries);

            command.Parameters.Add(new SqliteParameter($"${_snakeConv.ConvertName(nameof(amount))}", amount));

            ExecuteQueryWithoutResult(command);
        }

        public void VacuumDatabase()
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(CacheEntryDao)} has been disposed.");
            
            using var command = new SqliteCommand(Queries.VacuumDatabase);
            
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
            
            _connectionPool.Dispose();
        }

        private void CreateDatabase()
        {
            try
            {
                using var command = new SqliteCommand(Queries.CreateDatabase);

                ExecuteQueryWithoutResult(command);
            }
            catch
            {
                // Ignore
            }
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