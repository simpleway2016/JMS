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
        internal List<string> LockedKeys = new List<string>();
        public KeyLocker(MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
        }
        public bool TryLock(string key, bool waitToSuccess)
        {
            using (var netclient = new NetClient(_microServiceHost.MasterGatewayAddress.Address, _microServiceHost.MasterGatewayAddress.Port))
            {
                netclient.WriteServiceData(new GatewayCommand { 
                    Type = CommandType.LockKey,
                    Content = new LockKeyInfo { 
                         Key = key,
                          MicroServiceId = _microServiceHost.Id,
                           WaitToSuccess = waitToSuccess
                    }.ToJsonString()
                });

                var ret = netclient.ReadServiceObject<InvokeResult>();
                if(ret.Success)
                {
                    lock (LockedKeys)
                    {
                        LockedKeys.Add(key);
                    }
                }
                return ret.Success;
            }
        }

        public void UnLock(string key)
        {
            if (LockedKeys.Contains(key) == false)
                return;

            using (var netclient = new NetClient(_microServiceHost.MasterGatewayAddress.Address, _microServiceHost.MasterGatewayAddress.Port))
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

                var ret = netclient.ReadServiceObject<InvokeResult>();

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
