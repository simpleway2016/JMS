using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;

namespace JMS.Impls
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
                return _gatewayConnector??= _microServiceHost.ServiceProvider.GetService<IGatewayConnector>();
            }
        }
        public List<string> LockedKeys { get; }
        public string[] GetLockedKeys() => LockedKeys.ToArray();
        public KeyLocker(MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
            this.LockedKeys = new List<string>();
        }
        public bool TryLock(string key, bool waitToSuccess)
        {
            using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
            {
                netclient.WriteServiceData(new GatewayCommand { 
                    Type = CommandType.LockKey,
                    Content = new LockKeyInfo { 
                         Key = key,
                          MicroServiceId = _microServiceHost.Id,
                           WaitToSuccess = waitToSuccess
                    }.ToJsonString()
                });

                var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                if (ret.Success)
                {
                    lock (LockedKeys)
                    {
                        LockedKeys.Add(key);
                    }
                }
                else if (ret.Data != null)
                    throw new Exception(ret.Data);
                return ret.Success;
            }
        }

        public void UnLock(string key)
        {
            if (LockedKeys.Contains(key) == false)
                return;

            using (var netclient = GatewayConnector.CreateClient(_microServiceHost.MasterGatewayAddress))
            {
                netclient.WriteServiceData(new GatewayCommand
                {
                    Type = CommandType.LockKey,
                    Content = new LockKeyInfo
                    {
                        Key = key,
                        MicroServiceId = _microServiceHost.Id,
                        IsUnlock = true
                    }.ToJsonString()
                });

                var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                if (!ret.Success && ret.Data != null)
                    throw new Exception(ret.Data);

                lock (LockedKeys)
                {
                    if (LockedKeys.Contains(key))
                    {
                        LockedKeys.Remove(key);
                    }
                }
            }
        }
    }
}
