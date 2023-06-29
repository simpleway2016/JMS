using JMS;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTest.ServiceHosts
{
    public class UserInfoDbContext : IDbContext,IStorageEngine
    {
        bool _disposed;
        public bool BeganTransaction { get; private set; }

        public string UserName { get; set; }
        public int? Age { get; set; }
        public string Father { get; set; }
        public string Mather { get; set; }
        public static string FinallyUserName { get; set; }
        public static int FinallyAge { get; set; }
        public static string FinallyFather { get; set; }
        public static string FinallyMather { get; set; }

        public object CurrentTransaction => BeganTransaction ? this : null;

        public static int NewInstanceCount = 0;
        public static int CommitCount = 0;
        public UserInfoDbContext()
        {
            Interlocked.Increment(ref NewInstanceCount);
            Debug.WriteLine("UserInfoDbContext实例化");
        }

        public static void Reset()
        {
            CommitCount = 0;
            NewInstanceCount = 0;
            FinallyUserName = null;
            FinallyAge = 0;
            FinallyFather = null;
            FinallyMather = null;
        }

        public void BeginTransaction()
        {
            if (_disposed)
                throw new Exception("UserInfoDbContext已经释放了");
            if (!BeganTransaction)
            {
                BeganTransaction = true;
            }
        }

        public void CommitTransaction()
        {
            if (_disposed)
                throw new Exception("UserInfoDbContext已经释放了");
            if (BeganTransaction)
            {
                if(this.UserName != null)
                    FinallyUserName = this.UserName;

                if (this.Age != null)
                    FinallyAge = this.Age.Value;

                if (this.Father != null)
                    FinallyFather = this.Father;

                if (this.Mather != null)
                    FinallyMather = this.Mather;

                Debug.WriteLine("UserInfoDbContext提交事务了");
                Interlocked.Increment(ref CommitCount);
                BeganTransaction = false;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            Debug.WriteLine("UserInfoDbContext释放了");
        }

        public void RollbackTransaction()
        {
            if (_disposed)
                throw new Exception("UserInfoDbContext已经释放了");

            if (BeganTransaction)
            {
                Debug.WriteLine("UserInfoDbContext回滚事务了");
                BeganTransaction = false;
            }
        }

    }
}
