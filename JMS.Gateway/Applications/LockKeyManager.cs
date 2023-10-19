using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib.ECC;
using Microsoft.Extensions.DependencyInjection;
using JMS.Cluster;

namespace JMS.Applications
{
    public class LockKeyManager
    {
        ClusterGatewayConnector _clusterGC;
        IRegisterServiceManager _registerServiceManager;
        ConcurrentDictionary<string, KeyObject> _cache;
        Gateway _gateway;
        IConfiguration _configuration;
        int _timeout;
        ILogger<LockKeyManager> _logger;

        ClusterGatewayConnector ClusterGatewayConnector
        {
            get
            {
                return _clusterGC ??= _gateway.ServiceProvider.GetService<ClusterGatewayConnector>();
            }
        }

        public event EventHandler<KeyObject> LockKey;
        public event EventHandler<KeyObject> UnlockKey;

        public int KeyTimeout => _timeout;
        public LockKeyManager(Gateway gateway, IConfiguration configuration, IRegisterServiceManager registerServiceManager,
            ILogger<LockKeyManager> logger)
        {
            _registerServiceManager = registerServiceManager;
            _timeout = configuration.GetValue<int>("UnLockKeyTimeout");
            _cache = new ConcurrentDictionary<string, KeyObject>();
            _gateway = gateway;
            _configuration = configuration;
            _logger = logger;

            _registerServiceManager.ServiceConnect += _registerServiceManager_ServiceConnect;
            _registerServiceManager.ServiceDisconnect += _registerServiceManager_ServiceDisconnect;

            new Thread(checkTimeout).Start();
        }

        public KeyObject[] GetAllKeys()
        {
            return _cache.Values.ToArray();
        }

        public void AddKey(string key, string locker)
        {
            if (_cache.TryGetValue(key, out KeyObject keyObj) == false)
            {
                keyObj = new KeyObject()
                {
                    Key = key,
                    Locker = locker,
                };

                _cache.TryAdd(key, keyObj);
            }
            else if (_clusterGC.IsMaster == false)
            {
                //如果自己不是主网关，那么addkey应该是主网关发过来的
                keyObj.Locker = locker;
            }
        }

        public void RemoveKey(string key)
        {
            _cache.TryRemove(key, out KeyObject o);
        }

        /// <summary>
        /// 重置所有key过期时间
        /// </summary>
        public void ResetAllKeyExpireTime()
        {
            _logger?.LogDebug("设置所有lock对象的有效期");
            foreach (var pair in _cache)
            {
                var obj = pair.Value;
                obj.RemoveTime = DateTime.Now.AddMilliseconds(_timeout);
            }
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
            while (!_gateway.Disposed)
            {
                try
                {
                    foreach (var pair in _cache)
                    {
                        var obj = pair.Value;
                        if (obj.Locker != null && !_gateway.Disposed)
                        {
                            if (obj.RemoveTime != null && DateTime.Now >= obj.RemoveTime.GetValueOrDefault())
                            {
                                if (_cache.TryRemove(obj.Key, out obj))
                                {
                                    if (UnlockKey != null)
                                    {
                                        UnlockKey(this, obj);
                                    }
                                    _logger?.LogInformation($"key:{obj.Key}超时被unlock");
                                }
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

        public bool TryRelock(string key, RegisterServiceInfo locker)
        {
            if (_cache.TryGetValue(key, out KeyObject keyObj))
            {
                if (keyObj.Locker == locker.ServiceId)
                {
                    keyObj.RemoveTime = null;
                    return true;
                }
            }

            return false;
        }

        public bool TryLock(string key, RegisterServiceInfo locker)
        {

            if (_cache.TryGetValue(key, out KeyObject keyObj) == false)
            {
                KeyObject newKey = new KeyObject()
                {
                    Key = key,
                    Locker = locker.ServiceId
                };

                if (_cache.TryAdd(key, newKey))
                {
                    if (LockKey != null)
                    {
                        LockKey(this, newKey);
                    }

                    return true;
                }
            }
            else
            {
                if (keyObj.Locker == locker.ServiceId)
                {
                    keyObj.RemoveTime = null;
                    if (LockKey != null)
                    {
                        LockKey(this, keyObj);
                    }
                    return true;
                }
            }

            return false;

        }
        public void UnLockServiceAllKey(RegisterServiceInfo service)
        {
            KeyObject o;
            foreach (var pair in _cache)
            {
                if (pair.Value.Locker == service.ServiceId)
                {
                    _cache.TryRemove(pair.Key, out o);
                }
            }
        }


        public bool UnLock(string key, RegisterServiceInfo service)
        {
            if (_cache.TryGetValue(key, out KeyObject keyObj))
            {
                if (service == null || keyObj.Locker == service.ServiceId)
                {
                    if (_cache.TryRemove(key, out keyObj))
                    {
                        if (UnlockKey != null)
                        {
                            UnlockKey(this, keyObj);
                        }
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }
    }

    public class KeyObject
    {
        public string Key;
        public string Locker;
        /// <summary>
        /// 设定移除时间
        /// </summary>
        public DateTime? RemoveTime;
    }
}
