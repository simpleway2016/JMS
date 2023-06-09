using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Transactions;

namespace JMS.Domains
{
    class KeyLocker : IKeyLocker
    {
        MicroServiceHost _microServiceHost;

        //IGatewayConnector不能依赖注入，因为GatewayConnector依赖注入了KeyLocker
        IGatewayConnector _gatewayConnector;
        IGatewayConnector GatewayConnector
        {
            get
            {
                if(_gatewayConnector == null)
                {
                    _gatewayConnector = _microServiceHost.ServiceProvider.GetService<IGatewayConnector>();
                }
                return _gatewayConnector;
            }
        }
        ConcurrentDictionary<string,string> LockedKeyDict { get; }
        ConcurrentDictionary<string, string> RemovingKeyDict { get; }
        public string[] GetLockedKeys() => LockedKeyDict.Keys.Where( m=> RemovingKeyDict.Keys.Contains(m) == false).ToArray();
        /// <summary>
        /// 单位毫秒
        /// </summary>
        int _gatewayKeyTimeout;
        public KeyLocker(MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
            this.LockedKeyDict = new ConcurrentDictionary<string, string>();
            RemovingKeyDict = new ConcurrentDictionary<string, string>();
        }

        public void RemoveKeyFromLocal(string key)
        {
            LockedKeyDict.TryRemove(key, out string v);
            RemovingKeyDict.TryRemove(key, out v);
        }
        public bool TryLock(string transactionId, string key)
        {
            if (_microServiceHost.MasterGatewayAddress == null)
                throw new MissMasterGatewayException("未连接上主网关");
            if (string.IsNullOrEmpty(transactionId))
                throw new Exception("tranid is empty");

            LockedKeyDict.TryGetValue(key, out string lockerTranId);
            if (lockerTranId == transactionId)
                return true;

            if (LockedKeyDict.TryAdd(key, transactionId))
            {
                try
                {
                    using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
                    {

                        netclient.WriteServiceData(new GatewayCommand
                        {
                            Type = (int)CommandType.LockKey,
                            Content = new LockKeyInfo
                            {
                                Key = key,
                                MicroServiceId = _microServiceHost.Id,
                            }.ToJsonString()
                        });

                        var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                        if (ret.Success == false)
                        {
                            LockedKeyDict.TryRemove(key, out transactionId);
                        }

                        //记录网关的超时时间
                        if (ret.Success)
                            _gatewayKeyTimeout = Convert.ToInt32(ret.Data);

                        return ret.Success;
                    }
                }
                catch (Exception)
                {
                    LockedKeyDict.TryRemove(key, out transactionId);
                    throw;
                }
            }

            return false;
        }

        public async Task<bool> TryLockAsync(string transactionId, string key)
        {
            if (_microServiceHost.MasterGatewayAddress == null)
                throw new MissMasterGatewayException("未连接上主网关");
            if (string.IsNullOrEmpty(transactionId))
                throw new Exception("tranid is empty");

            LockedKeyDict.TryGetValue(key, out string lockerTranId);
            if (lockerTranId == transactionId)
                return true;

            if (LockedKeyDict.TryAdd(key, transactionId))
            {
                try
                {
                    using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
                    {

                        netclient.WriteServiceData(new GatewayCommand
                        {
                            Type = (int)CommandType.LockKey,
                            Content = new LockKeyInfo
                            {
                                Key = key,
                                MicroServiceId = _microServiceHost.Id,
                            }.ToJsonString()
                        });

                        var ret = await netclient.ReadServiceObjectAsync<InvokeResult<string>>();
                        if (ret.Success == false)
                        {
                            LockedKeyDict.TryRemove(key, out transactionId);
                        }

                        //记录网关的超时时间
                        if (ret.Success)
                            _gatewayKeyTimeout = Convert.ToInt32(ret.Data);

                        return ret.Success;
                    }
                }
                catch (Exception)
                {
                    LockedKeyDict.TryRemove(key, out transactionId);
                    throw;
                }
            }

            return false;
        }

        /// <summary>
        /// unlock本服务所有key
        /// </summary>
        public void UnLockAllKeys()
        {
            using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
            {
                netclient.WriteServiceData(new GatewayCommand
                {
                    Type = (int)CommandType.LockKey,
                    Content = new LockKeyInfo
                    {
                        Key = null,
                        MicroServiceId = _microServiceHost.Id,
                        IsUnlock = true
                    }.ToJsonString()
                });

                var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                if (ret.Success)
                {
                    LockedKeyDict.Clear();
                    RemovingKeyDict.Clear();
                }

                if (!ret.Success && ret.Data != null)
                    throw new Exception(ret.Data);
            }
        }

        public bool TryUnLock(string transactionId, string key)
        {
            if(key == null)
                throw new Exception("key is null");
            if (_microServiceHost.MasterGatewayAddress == null)
                throw new MissMasterGatewayException("未连接上主网关");
            if (string.IsNullOrEmpty(transactionId))
                throw new Exception("tranid is empty");

            if (LockedKeyDict.TryGetValue(key, out string locker))
            {
                if (locker == transactionId)
                {
                    RemovingKeyDict.TryAdd(key, transactionId);
                    DateTime startime = DateTime.Now;
                    while (true)
                    {
                        try
                        {
                            //如果连接网关失败
                            using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
                            {
                                netclient.WriteServiceData(new GatewayCommand
                                {
                                    Type = (int)CommandType.LockKey,
                                    Content = new LockKeyInfo
                                    {
                                        Key = key,
                                        MicroServiceId = _microServiceHost.Id,
                                        IsUnlock = true
                                    }.ToJsonString()
                                });

                                var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                                LockedKeyDict.TryRemove(key, out transactionId);
                                RemovingKeyDict.TryRemove(key, out transactionId);

                                if (!ret.Success && ret.Data != null)
                                    throw new Exception(ret.Data);
                                return ret.Success;
                            }
                           
                            break;
                        }
                        catch (Exception)
                        {
                            //如果发生错误，可以不断重试，直到超时为止
                            if((DateTime.Now - startime).TotalMilliseconds > _gatewayKeyTimeout)
                            {
                                //如果已经连不上网关，网关会在10秒内释放这个key
                                LockedKeyDict.TryRemove(key, out transactionId);
                                RemovingKeyDict.TryRemove(key, out transactionId);
                                throw;
                            }
                            else
                            {
                                Thread.Sleep(1000);
                            }
                        }
                    }
                   
                }
            }
            return false;
        }

        public async Task<bool> TryUnLockAsync(string transactionId, string key)
        {
            if (key == null)
                throw new Exception("key is null");
            if (_microServiceHost.MasterGatewayAddress == null)
                throw new MissMasterGatewayException("未连接上主网关");
            if (string.IsNullOrEmpty(transactionId))
                throw new Exception("tranid is empty");

            if (LockedKeyDict.TryGetValue(key, out string locker))
            {
                if (locker == transactionId)
                {
                    RemovingKeyDict.TryAdd(key, transactionId);
                    DateTime startime = DateTime.Now;
                    while (true)
                    {
                        try
                        {
                            //如果连接网关失败
                            using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
                            {
                                netclient.WriteServiceData(new GatewayCommand
                                {
                                    Type = (int)CommandType.LockKey,
                                    Content = new LockKeyInfo
                                    {
                                        Key = key,
                                        MicroServiceId = _microServiceHost.Id,
                                        IsUnlock = true
                                    }.ToJsonString()
                                });

                                var ret = await netclient.ReadServiceObjectAsync<InvokeResult<string>>();
                                LockedKeyDict.TryRemove(key, out transactionId);
                                RemovingKeyDict.TryRemove(key, out transactionId);

                                if (!ret.Success && ret.Data != null)
                                    throw new Exception(ret.Data);
                                return ret.Success;
                            }

                            break;
                        }
                        catch (Exception)
                        {
                            //如果发生错误，可以不断重试，直到超时为止
                            if ((DateTime.Now - startime).TotalMilliseconds > _gatewayKeyTimeout)
                            {
                                //如果已经连不上网关，网关会在10秒内释放这个key
                                LockedKeyDict.TryRemove(key, out transactionId);
                                RemovingKeyDict.TryRemove(key, out transactionId);
                                throw;
                            }
                            else
                            {
                                Thread.Sleep(1000);
                            }
                        }
                    }

                }
            }
            return false;
        }

        public void UnLockAnyway(string key)
        {
            if (key == null)
                throw new Exception("key is null");
            if (_microServiceHost.MasterGatewayAddress == null)
                throw new MissMasterGatewayException("未连接上主网关");

            var transactionId = "";
            RemovingKeyDict.TryAdd(key, transactionId);
            DateTime startime = DateTime.Now;
            while (true)
            {
                try
                {
                    //如果连接网关失败
                    using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
                    {
                        netclient.WriteServiceData(new GatewayCommand
                        {
                            Type = (int)CommandType.LockKey,
                            Content = new LockKeyInfo
                            {
                                Key = key,
                                MicroServiceId = "$$$",//表示强制释放
                                IsUnlock = true
                            }.ToJsonString()
                        });

                        var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                        if (!ret.Success && ret.Data != null)
                            throw new Exception(ret.Data);
                    }
                    LockedKeyDict.TryRemove(key, out transactionId);
                    RemovingKeyDict.TryRemove(key, out transactionId);
                    break;
                }
                catch (Exception)
                {
                    //如果发生错误，可以不断重试，直到超时为止
                    if ((DateTime.Now - startime).TotalMilliseconds > _gatewayKeyTimeout)
                    {
                        //如果已经连不上网关，网关会在10秒内释放这个key
                        LockedKeyDict.TryRemove(key, out transactionId);
                        RemovingKeyDict.TryRemove(key, out transactionId);
                        throw;
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        public async Task UnLockAnywayAsync(string key)
        {
            if (key == null)
                throw new Exception("key is null");
            if (_microServiceHost.MasterGatewayAddress == null)
                throw new MissMasterGatewayException("未连接上主网关");

            var transactionId = "";
            RemovingKeyDict.TryAdd(key, transactionId);
            DateTime startime = DateTime.Now;
            while (true)
            {
                try
                {
                    //如果连接网关失败
                    using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
                    {
                        netclient.WriteServiceData(new GatewayCommand
                        {
                            Type = (int)CommandType.LockKey,
                            Content = new LockKeyInfo
                            {
                                Key = key,
                                MicroServiceId = "$$$",//表示强制释放
                                IsUnlock = true
                            }.ToJsonString()
                        });

                        var ret = await netclient.ReadServiceObjectAsync<InvokeResult<string>>();
                        if (!ret.Success && ret.Data != null)
                            throw new Exception(ret.Data);
                    }
                    LockedKeyDict.TryRemove(key, out transactionId);
                    RemovingKeyDict.TryRemove(key, out transactionId);
                    break;
                }
                catch (Exception)
                {
                    //如果发生错误，可以不断重试，直到超时为止
                    if ((DateTime.Now - startime).TotalMilliseconds > _gatewayKeyTimeout)
                    {
                        //如果已经连不上网关，网关会在10秒内释放这个key
                        LockedKeyDict.TryRemove(key, out transactionId);
                        RemovingKeyDict.TryRemove(key, out transactionId);
                        throw;
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
        }
    }
}
