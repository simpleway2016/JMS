using JMS.Impls;
using JMS.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    class LockKeyManager
    {
        System.Collections.Concurrent.ConcurrentDictionary<string, KeyObject> _cache;
        Gateway _gateway;
        IConfiguration _configuration;
        int timeout;
        ILogger<LockKeyManager> _logger;
        public LockKeyManager(Gateway gateway,IConfiguration configuration,ILogger<LockKeyManager> logger)
        {
            _cache = new System.Collections.Concurrent.ConcurrentDictionary<string, KeyObject>();
            _gateway = gateway;
            _configuration = configuration;
            _logger = logger;

            timeout = configuration.GetValue<int>("UnLockKeyTimeout");

            new Thread(checkTimeout).Start();
        }

        private void checkTimeout()
        {
            while(true)
            {
                try
                {
                    var keys = _cache.Keys.ToArray();
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var obj = _cache[keys[i]];
                        if (obj.Locker > 0)
                        {
                            if (obj.LockTime != null)
                            {
                                if((DateTime.Now - obj.LockTime.Value).TotalMilliseconds > timeout)
                                {
                                    _cache.TryRemove(obj.Key, out obj);
                                    _logger?.LogInformation($"key:{obj.Key}超时被unlock");
                                }
                            }
                            else
                            {
                                obj.LockTime = DateTime.Now;
                            }
                        }
                       
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                }
                finally
                {
                    Thread.Sleep(2000);
                }
            }
        }

        public bool TryLock(string key, IMicroServiceReception locker)
        {
            KeyObject keyObj = null;
            while (true)
            {
                if (_cache.TryGetValue(key, out keyObj) == false)
                {
                    if (_cache.TryAdd(key, new KeyObject()
                    {
                        Key = key,
                        Locker = locker.Id,
                        LockTime = DateTime.Now
                    }))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    if(keyObj.Locker == locker.Id)
                    {
                        //同一个微服务进行时间更新
                        keyObj.LockTime = DateTime.Now;
                        return true;
                    }
                    if (Interlocked.CompareExchange(ref keyObj.Locker, locker.Id, 0) == 0)
                    {
                        keyObj.LockTime = DateTime.Now;
                        return true;
                    }
                    else
                        return false;
                }
            }
        }

        public void UnLock(string key,IMicroServiceReception service)
        {
            KeyObject keyObj = null;
            if (_cache.TryGetValue(key, out keyObj)  )
            {
                if (keyObj.Locker == service.Id)
                {
                    _cache.TryRemove(key, out keyObj);
                }
            }
        }
    }

    class KeyObject
    {
        public string Key;
        /// <summary>
        /// 不为0:locked 0:unlocked
        /// </summary>
        public int Locker;
        public DateTime? LockTime;
    }
}
