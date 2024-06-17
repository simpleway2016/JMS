using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Common
{
    public class BlackList
    {
        /// <summary>
        /// 最多错误次数
        /// </summary>
        public const int MaxErrorCount = 10;
        /// <summary>
        /// 拉黑多长时间（单位/分钟）
        /// </summary>
        int _KeepMinutes = 30;
        ConcurrentDictionary<string, BlackListItem> _UserItems = new ConcurrentDictionary<string, BlackListItem>();

        /// <summary>
        /// 设置拉黑多长时间，默认30分钟
        /// </summary>
        /// <param name="minutes"></param>
        public void SetKeepMinutes(int minutes)
        {
            _KeepMinutes = minutes;
        }

        /// <summary>
        /// 记录错误了1次
        /// </summary>
        /// <param name="key"></param>
        /// <returns>剩余几次机会</returns>
        public int MarkError(string key)
        {
            var item = _UserItems.GetOrAdd(key, k => new BlackListItem());
            item.MarkError();
            return Math.Max(0, MaxErrorCount - item.ErrorCount);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        public void ClearError(string key)
        {
            _UserItems.TryRemove(key, out _);
        }

        /// <summary>
        /// 检查是否允许通过
        /// </summary>
        /// <param name="key"></param>
        /// <returns>false=不能通过</returns>
        public bool CheckBlackList(string key)
        {
            if (_UserItems.TryGetValue(key, out BlackListItem item))
            {
                if ((DateTime.Now - item.UpdateTime).TotalMinutes >= _KeepMinutes)
                {
                    ClearError(key);
                    return true;
                }
                if (item.ErrorCount >= MaxErrorCount)
                {
                    item.MarkError();
                    return false;
                }
            }

            return true;
        }
    }
}
