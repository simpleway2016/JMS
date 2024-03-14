using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest.ServiceHosts
{
    internal interface IDbContext:IDisposable
    {
        bool BeganTransaction { get; }
        void BeginTransaction();
        void CommitTransaction();
        void RollbackTransaction();
    }
}
