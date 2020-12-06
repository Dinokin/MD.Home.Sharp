using System;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.Sqlite;

namespace MD.Home.Sharp.Cache
{
    internal class ConnectionPool : IDisposable
    {
        private readonly string _connectionString;
        private readonly ulong _cacheSize;
        private readonly ConcurrentQueue<SqliteConnection> _pool;

        private bool _isDisposed;

        public ConnectionPool(string connectionString, ulong cacheSizeInKibibytes)
        {
            _connectionString = connectionString;
            _cacheSize = cacheSizeInKibibytes;
            _pool = new ConcurrentQueue<SqliteConnection>();
        }
        
        public SqliteConnection GetConnection()
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(ConnectionPool)} has been disposed.");

            return _pool.TryDequeue(out var connection) ? connection : BuildConnection();
        }
        
        public void ReturnConnection(SqliteConnection connection)
        {
            if (_isDisposed || connection.State != ConnectionState.Open)
                DestroyConnection(connection);
            else 
                _pool.Enqueue(connection);
        }
       
        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(ConnectionPool)} has been disposed.");
                
                _isDisposed = true;
            }
            
            while (_pool.TryDequeue(out var connection))
                DestroyConnection(connection);
            
            GC.SuppressFinalize(this);
        }

        private SqliteConnection BuildConnection()
        {
            var connection = new SqliteConnection(_connectionString) {DefaultTimeout = 90};
            connection.Open();

            using var command = new SqliteCommand(Queries.SetCacheSize, connection);
            command.Parameters.Add(new SqliteParameter("$size", _cacheSize));
            
            command.ExecuteNonQuery();

            return connection;
        }

        private static void DestroyConnection(IDbConnection connection)
        {
            try
            {
                connection.Close();
                connection.Dispose();
            }
            catch
            {
                // Ignore
            }
        }
    }
}