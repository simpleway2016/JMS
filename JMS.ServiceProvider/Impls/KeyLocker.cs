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
        MicroServiceHost _microServiceProvider;

        public KeyLocker(MicroServiceHost microServiceProvider)
        {
            _microServiceProvider = microServiceProvider;
        }
        public bool TryLock(string key, bool waitToSuccess)
        {
            using (var netclient = new NetClient(_microServiceProvider.GatewayAddress,_microServiceProvider.GatewayPort))
            {
                netclient.WriteServiceData(new GatewayCommand { 
                    Type = CommandType.LockKey,
                    Content = new LockKeyInfo { 
                         Key = key,
                          MicroServiceId = _microServiceProvider.Id,
                           WaitToSuccess = waitToSuccess
                    }.ToJsonString()
                });

                var ret = netclient.ReadServiceObject<InvokeResult>();
                return ret.Success;
            }
        }

        public void UnLock(string key)
        {
            using (var netclient = new NetClient(_microServiceProvider.GatewayAddress, _microServiceProvider.GatewayPort))
            {
                netclient.WriteServiceData(new GatewayCommand
                {
                    Type = CommandType.LockKey,
                    Content = new LockKeyInfo
                    {
                        Key = key,
                        MicroServiceId = _microServiceProvider.Id,
                        IsUnlock = true
                    }.ToJsonString()
                });

                var ret = netclient.ReadServiceObject<InvokeResult>();
            }
        }
    }
}
