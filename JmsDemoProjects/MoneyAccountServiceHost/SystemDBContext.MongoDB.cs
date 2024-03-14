//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Text;
//using System.Threading;
//using Microsoft.Data.Sqlite;
//using MongoDB.Driver;

//namespace MoneyAccountServiceHost
//{
//    public class SystemDBContext : IDisposable, JMS.IStorageEngine
//    {
//        MongoClient _mongoClient;

//        public MongoClient MongoClient => _mongoClient;

//        public SystemDBContext()
//        {
//            _mongoClient = new MongoClient("mongodb://192.168.11.137:27017");
//        }

//        public IClientSessionHandle Session { get; set; }

//        object IStorageEngine.CurrentTransaction => this.Session;

//        public void BeginTransaction()
//        {
//            if (this.Session == null){
//                this.Session = _mongoClient.StartSession();
//                this.Session.StartTransaction();
//            }
//        }
//        public void CommitTransaction()
//        {
//            if (this.Session != null)
//            {
//                this.Session.CommitTransaction();
//                this.Session.Dispose();
//                this.Session = null;
//            }
//        }
//        public void RollbackTransaction()
//        {
//            if (this.Session != null)
//            {
//                this.Session.AbortTransaction();
//                this.Session.Dispose();
//                this.Session = null;
//            }
//        }

//        public void Dispose()
//        {
//            RollbackTransaction();
//        }
//    }
//}

