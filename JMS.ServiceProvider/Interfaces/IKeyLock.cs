using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    public interface IKeyLocker
    {
        List<string> LockedKeys { get; }
        /// <summary>
        /// 申请锁住指定的key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="waitToSuccess">等到成功为止</param>
        /// <returns>是否成功</returns>
        bool TryLock(string key,bool waitToSuccess);
        void UnLock(string key);
    }
}
