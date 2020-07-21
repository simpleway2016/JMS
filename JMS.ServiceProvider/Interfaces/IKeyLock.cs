using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    public interface IKeyLocker
    {
        /// <summary>
        /// 申请锁住指定的key
        /// </summary>
        /// <param name="transactionId">在controller中，事务id属于纯数字类型，如果不是在controller中调用lock，事务id可以用字母+数字组合，避免和其他请求重复</param>
        /// <param name="key"></param>
        /// <param name="waitToSuccess">等到成功为止</param>
        /// <returns>是否成功</returns>
        bool TryLock(string transactionId, string key,bool waitToSuccess);
        void UnLock(string transactionId, string key);
        string[] GetLockedKeys();
        
    }
}
