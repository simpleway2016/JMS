using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace JMS.MicroServiceHost
{
    /// <summary>
    /// 这只是一个假的dbcontext,只是用来示范如何控制事务
    /// 你可以按实际情况，编写一个类似的数据库操作对象
    /// </summary>
    public class SystemDBContext : IDisposable, JMS.IStorageEngine
    {

        /// <summary>
        /// 当前事务对象
        /// </summary>
        public object CurrentTransaction { get; set; }

        public void BeginTransaction()
        {
            if (this.CurrentTransaction == null)
            {
                this.CurrentTransaction = new object();
            }

        }
        public void CommitTransaction()
        {
            if (this.CurrentTransaction != null)
            {
                this.CurrentTransaction = null;
            }

        }
        public void RollbackTransaction()
        {
            if (this.CurrentTransaction != null)
            {
                this.CurrentTransaction = null;
            }
        }

        public void Dispose()
        {
            if (this.CurrentTransaction != null)
            {
                RollbackTransaction();
            }
            this.CurrentTransaction = null;
        }
    }
}
