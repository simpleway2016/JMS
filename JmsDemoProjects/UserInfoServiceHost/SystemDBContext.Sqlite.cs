using JMS;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace UserInfoServiceHost
{
    public class SystemDBContext : IDisposable, IStorageEngine
    {
        SqliteConnection _connection;

        public SystemDBContext()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("data source=./data.db");
            _connection.Open();
        }

        public SqliteConnection Connection => _connection;

        /// <summary>
        /// 当前事务对象
        /// </summary>
        public object CurrentTransaction { get; set; }
        SqliteTransaction _transaction;

        public void BeginTransaction()
        {
            if (CurrentTransaction == null)
                this.CurrentTransaction = _transaction = _connection.BeginTransaction();
        }
        public void CommitTransaction()
        {
            _transaction?.Commit();
            this.CurrentTransaction = null;
        }
        public void RollbackTransaction()
        {
            _transaction?.Rollback();
            this.CurrentTransaction = null;
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            this.CurrentTransaction = _transaction = null;
            _connection.Dispose();
        }
    }
}
