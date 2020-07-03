using JMS.Impls;
using JMS.Interfaces;
using Microsoft.Extensions.Configuration;
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
        public LockKeyManager(Gateway gateway,IConfiguration configuration)
        {
            _cache = new System.Collections.Concurrent.ConcurrentDictionary<string, KeyObject>();
            _gateway = gateway;
            _configuration = configuration;
            _gateway.OnlineMicroServices.CollectionChanged += OnlineMicroServices_CollectionChanged;
            timeout = configuration.GetValue<int>("UnLockKeyTimeout");
        }

        private void OnlineMicroServices_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                var removedItems = e.OldItems;
                foreach(IMicroServiceReception service in removedItems)
                {
                    var keys = _cache.Keys.ToArray();
                    for (int i = 0; i < keys.Length; i ++)
                    {
                        var obj = _cache[keys[i]];
                        if(obj.Locker == service.Id)
                        {
                            //考虑离线需要释放锁
                            Task.Run(()=> {
                                //等待10秒，如果还是没上线，释放锁
                                Thread.Sleep(timeout);
                                if( _gateway.GetServiceById(obj.Locker) == null )
                                {
                                    //释放锁
                                    obj.Locker = 0;
                                }
                            });
                        }
                    }
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
                        Locker = locker.Id
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
                    if (Interlocked.CompareExchange(ref keyObj.Locker, locker.Id, 0) == 0)
                    {
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
                    keyObj.Locker = 0;
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
    }
}
