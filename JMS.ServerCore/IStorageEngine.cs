using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public interface IStorageEngine
    {
        /// <summary>
        /// 当前事务对象
        /// </summary>
        object CurrentTransaction{ get; }
        /// <summary>
        /// 启动事务
        /// </summary>
        void BeginTransaction();
        /// <summary>
        /// 提交事务
        /// </summary>
        void CommitTransaction();
        /// <summary>
        /// 回滚事务
        /// </summary>
        void RollbackTransaction();
    }
}
