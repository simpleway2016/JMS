using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest.ServiceHosts
{
    internal class UserInfoDbContext : IDbContext
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

        public UserInfoDbContext()
        {
            Debug.WriteLine("UserInfoDbContext实例化");
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
