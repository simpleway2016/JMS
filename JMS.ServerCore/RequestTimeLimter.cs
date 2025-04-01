using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.ServerCore
{
    public class RequestTimeLimter
    {
        Common.ConfigurationValue<LimitSetting> _limitSetting;
        ConcurrentDictionary<string, RequestRecord> _ipRecords = new ConcurrentDictionary<string, RequestRecord>();
        public Common.ConfigurationValue<LimitSetting> LimitSetting => _limitSetting;
        public RequestTimeLimter(IConfiguration configuration)
        {
            _limitSetting = configuration.GetSection("RequestTime").GetNewest<LimitSetting>();
            _ = clearNotUseIp();
        }

        /// <summary>
        /// 定期清理不用的ip
        /// </summary>
        async Task clearNotUseIp()
        {
            while (true)
            {
                await Task.Delay(60000);
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
            if (_limitSetting.Current != null && LimitSetting.Current.Limit > 0)
            {
                var record = _ipRecords.GetOrAdd(ip, k => new RequestRecord() { StartTime = DateTime.Now });
                var locktime = record.LockTime;
                if (locktime != null && (DateTime.Now - locktime.Value).TotalMinutes < _limitSetting.Current.LockMinutes)
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
                    if (ret > _limitSetting.Current.Limit)
                    {
                        record.LockTime = DateTime.Now;
                        return false;
                    }
                }

            }

            return true;
        }

        static string[] LoopbackIps = new[] { "127.0.0.1", "::1" }; 
        /// <summary>
        /// 获取真实ip
        /// </summary>
        /// <param name="trustXForwardedFor">受信任的中转站ip</param>
        /// <param name="remoteIpAddr">当前请求ip</param>
        /// <param name="xForwardedfor">x-Forwarded-for请求头</param>
        /// <returns></returns>
        public static string GetRemoteIpAddress(string[] trustXForwardedFor, string remoteIpAddr, string xForwardedfor)
        {
            if (xForwardedfor != null)
            {
                bool isloopback = LoopbackIps.Contains(remoteIpAddr);
              
                if (isloopback || trustXForwardedFor?.Contains(remoteIpAddr) == true)
                {
                    var x_forArr = xForwardedfor.Split(',').Select(m => m.Trim()).Where(m => m.Length > 0).ToArray();
                    for (int i = x_forArr.Length - 1; i >= 0; i--)
                    {
                        var ip = x_forArr[i];
                        if (!LoopbackIps.Contains(ip) && trustXForwardedFor.Contains(ip) == false)
                            return ip;
                    }
                }
                else
                {
                   

                    return remoteIpAddr;
                }
            }

            return remoteIpAddr;
        }

    }
    public class RequestRecord
    {
        public DateTime StartTime;
        public long Count;
        public DateTime? LockTime;
    }

    public class LimitSetting
    {
        public int Limit { get; set; }
        public int LockMinutes { get; set; }
    }
}
