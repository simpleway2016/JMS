using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy
{
    public class RequestTimeLimter
    {
        LimitSetting _limitSetting;
        ConcurrentDictionary<string, RequestRecord> _ipRecords = new ConcurrentDictionary<string, RequestRecord>();
        public RequestTimeLimter(IConfiguration configuration)
        {
            _limitSetting = configuration.GetSection("RequestTime").Get<LimitSetting>();
            new Thread(clearNotUseIp).Start();
        }

        /// <summary>
        /// 定期清理不用的ip
        /// </summary>
        void clearNotUseIp()
        {
            while (true)
            {
                Thread.Sleep(60000);
                foreach (var pair in _ipRecords)
                {
                    if ((DateTime.Now - pair.Value.StartTime).TotalMinutes > 1)
                    {
                        _ipRecords.TryRemove(pair.Key, out _);
                    }
                }
            }
        }

        public bool OnRequesting(string ip)
        {
            if (_limitSetting != null)
            {
                var record = _ipRecords.GetOrAdd(ip, k => new RequestRecord() { StartTime = DateTime.Now });
                var locktime = record.LockTime;
                if (locktime != null && (DateTime.Now - locktime.Value).TotalMinutes < _limitSetting.LockMinutes)
                {
                    return false;
                }
                else if (locktime != null)
                {
                    record.LockTime = null;
                }

                if ((DateTime.Now - record.StartTime).TotalSeconds >= 1)
                {
                    record.StartTime = DateTime.Now;
                    record.Count = 1;
                }
                else
                {
                    var ret = Interlocked.Increment(ref record.Count);
                    if (ret > _limitSetting.Limit)
                    {
                        record.LockTime = DateTime.Now;
                        return false;
                    }
                }

            }

            return true;
        }
        class RequestRecord
        {
            public DateTime StartTime;
            public long Count;
            public DateTime? LockTime;
        }

        class LimitSetting
        {
            public int Limit { get; set; }
            public int LockMinutes { get; set; }
        }
    }
}
