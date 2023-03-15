using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.Domains
{
    /// <summary>
    /// 记录无授权的用户
    /// </summary>
    internal class ErrorUserMarker
    {
        /// <summary>
        /// 最大错误次数
        /// </summary>
        const int MaxError = 5;
        const int BlackListHours = 3;
        ConcurrentDictionary<string, ErrorItem> ErrCountDict = new ConcurrentDictionary<string, ErrorItem>();
        public bool CheckUserIp(string ip)
        {
            if (ErrCountDict.TryGetValue(ip, out ErrorItem errorItem))
            {
                if (errorItem.BlackListTime != null && DateTime.Now < errorItem.BlackListTime.Value)
                    return false;

                if (errorItem.BlackListTime != null && DateTime.Now >= errorItem.BlackListTime.Value)
                {
                    this.Clear(ip);
                }
            }

            return true;
        }

        public void Error(string ip)
        {
            if (ErrCountDict.TryGetValue(ip, out ErrorItem errorItem))
            {
                Interlocked.Increment(ref errorItem.Count);
                if (errorItem.Count >= MaxError)
                {
                    errorItem.BlackListTime = DateTime.Now.AddHours(BlackListHours);
                }
            }
            else
            {
                ErrCountDict[ip] = new ErrorItem
                {
                    Count = 1
                };
            }          
        }

        /// <summary>
        /// 清除错误记录
        /// </summary>
        /// <param name="ip"></param>
        public void Clear(string ip)
        {
            ErrCountDict.TryRemove(ip, out ErrorItem o);
        }

        class ErrorItem
        {
            public int Count;
            public DateTime? BlackListTime;
        }
    }
}
