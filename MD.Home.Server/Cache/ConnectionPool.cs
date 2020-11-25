using System;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.Sqlite;

namespace MD.Home.Server.Cache
{
    public class ConnectionPool : IDisposable
    {
        private readonly string _connectionString;
        private readonly ConcurrentQueue<SqliteConnection> _pool;

        private bool _isDisposed;

        public ConnectionPool(string connectionString, ushort poolSize)
        {
            _connectionString = connectionString;
            _pool = new ConcurrentQueue<SqliteConnection>();
            
            FillPool(poolSize <= 0 ? 1 : poolSize);
        }
        
        public SqliteConnection GetConnection()
        {
            if (_isDisposed)
                throw new ObjectDisposedException($"This instance of {nameof(ConnectionPool)} has been disposed.");

            SqliteConnection? connection;
            
            do 
                _pool.TryDequeue(out connection);
            while (connection == null);
            
            connection.Open();
            
            return connection;
        }
       
        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException($"This instance of {nameof(ConnectionPool)} has been disposed.");
                
                _isDisposed = true;
            }

            for (var i = 0; i < _pool.Count; i++)
            {
                if (_pool.TryDequeue(out var connection))
                    connection.Dispose();
            }
            
            GC.SuppressFinalize(this);
        }

        private SqliteConnection BuildConnection()
        {
            var connection = new SqliteConnection(_connectionString) {DefaultTimeout = 90};

            connection.StateChange += (sender, _) => ReturnConnection((SqliteConnection) sender);

            return connection;
        }

        private void FillPool(ushort poolSize)
        {
            for (var i = 0; i < poolSize; i++)
                _pool.Enqueue(BuildConnection());
        }

        private void ReturnConnection(SqliteConnection connection)
        {
            if (connection.State != ConnectionState.Closed)
                return;
            
            if (_isDisposed)
                connection.Dispose();
            
            _pool.Enqueue(connection);
        }
    }
}