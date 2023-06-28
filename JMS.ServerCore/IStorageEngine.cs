using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public interface IStorageEngine
    {
        /// <summary>
        /// 是否已开启事务
        /// </summary>
        /// <returns></returns>
        bool IsBeganTransaction();
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
