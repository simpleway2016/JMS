using JMS.Dtos;
using JMS.Domains;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.Domains
{
    class LockKeyManager
    {
        IRegisterServiceManager _registerServiceManager;
        System.Collections.Concurrent.ConcurrentDictionary<string, KeyObject> _cache;
        Gateway _gateway;
        IConfiguration _configuration;
        int _timeout;
        ILogger<LockKeyManager> _logger;
        internal bool IsReady;

        public int KeyTimeout => _timeout;
        public LockKeyManager(Gateway gateway, IConfiguration configuration, IRegisterServiceManager registerServiceManager, ILogger<LockKeyManager> logger)
        {
            this._registerServiceManager = registerServiceManager;
            _timeout = configuration.GetValue<int>("UnLockKeyTimeout");
            _cache = new System.Collections.Concurrent.ConcurrentDictionary<string, KeyObject>();
            _gateway = gateway;
            _configuration = configuration;
            _logger = logger;

            _registerServiceManager.ServiceConnect += _registerServiceManager_ServiceConnect;
            _registerServiceManager.ServiceDisconnect += _registerServiceManager_ServiceDisconnect;

            new Thread(checkTimeout).Start();
        }

        private void _registerServiceManager_ServiceDisconnect(object sender, RegisterServiceInfo e)
        {
            //把这个微服务的lockkey设置下线时间
            while (true)
            {
                try
                {
                    foreach (var pair in _cache)
                    {
                        var obj = pair.Value;
                        if (obj.Locker == e.ServiceId)
                        {
                            obj.RemoveTime = DateTime.Now.AddMilliseconds(_timeout);
                        }

                    }
                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(0);
                    continue;
                }

            }
        }

        private void _registerServiceManager_ServiceConnect(object sender, RegisterServiceInfo e)
        {
            while (true)
            {
                try
                {
                    foreach (var pair in _cache)
                    {
                        var obj = pair.Value;
                        if (obj.Locker == e.ServiceId)
                        {
                            obj.RemoveTime = null;
                        }

                    }
                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(0);
                    continue;
                }

            }
        }

        internal KeyObject[] GetCaches()
        {
           return _cache.Values.ToArray();
        }

        /// <summary>
        /// 检查已经断线的微服务
        /// </summary>
        private void checkTimeout()
        {
            while(true)
            {
                try
                {
                    foreach (var pair in _cache)
                    {
                        var obj = pair.Value;
                        if (obj.Locker != null)
                        {
                            if (obj.RemoveTime != null && DateTime.Now >= obj.RemoveTime.GetValueOrDefault())
                            {
                                _cache.TryRemove(obj.Key, out obj);
                                _logger?.LogInformation($"key:{obj.Key}超时被unlock");
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

        public bool TryLock(string key, RegisterServiceInfo locker,bool checkReady = true)
        {
            if (checkReady && IsReady == false)
                throw new Exception("lock key is not ready");

            KeyObject keyObj = null;
            while (true)
            {
                if (_cache.TryGetValue(key, out keyObj) == false)
                {
                    if (_cache.TryAdd(key, new KeyObject()
                    {
                        Key = key,
                        Locker = locker.ServiceId
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
                    if(keyObj.Locker == locker.ServiceId)
                    {
                        keyObj.RemoveTime = null;
                        return true;
                    }
                    else
                        return false;
                }
            }
        }

        public bool UnLock(string key,RegisterServiceInfo service)
        {
            if (IsReady == false)
                throw new Exception("lock key is not ready");

            if (_cache.TryGetValue(key, out KeyObject keyObj)  )
            {
                if (service == null || keyObj.Locker == service.ServiceId)
                {
                   return _cache.TryRemove(key, out keyObj);
                }
            }
            return false;
        }
    }

    class KeyObject
    {
        public string Key;
        public string Locker;
        /// <summary>
        /// 设定移除时间
        /// </summary>
        public DateTime? RemoveTime;
    }
}
