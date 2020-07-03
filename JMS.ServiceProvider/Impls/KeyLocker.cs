using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
namespace JMS.Impls
{
    class KeyLocker : IKeyLocker
    {
        MicroServiceProvider _microServiceProvider;

        private GatewayConnector _GatewayConnector;
        public GatewayConnector GatewayConnector
        {
            get
            {
                if(_GatewayConnector == null)
                    _GatewayConnector = _microServiceProvider.ServiceProvider.GetService<GatewayConnector>();
                return _GatewayConnector;
            }
        }
        public KeyLocker(MicroServiceProvider microServiceProvider)
        {
            _microServiceProvider = microServiceProvider;
        }
        public bool TryLock(string key, bool waitToSuccess)
        {
            using (var netclient = new Way.Lib.NetStream(_microServiceProvider.GatewayAddress,_microServiceProvider.GatewayPort))
            {
                netclient.ReadTimeout = 20000;
                netclient.WriteServiceData(new GatewayCommand { 
                    Type = CommandType.LockKey,
                    Content = new LockKeyInfo { 
                         Key = key,
                          MicroServiceId = _GatewayConnector.ServiceId,
                           WaitToSuccess = waitToSuccess
                    }.ToJsonString()
                });

                var ret = netclient.ReadServiceObject<InvokeResult>();
                return ret.Success;
            }
        }

        public void UnLock(string key)
        {
            using (var netclient = new Way.Lib.NetStream(_microServiceProvider.GatewayAddress, _microServiceProvider.GatewayPort))
            {
                netclient.ReadTimeout = 20000;
                netclient.WriteServiceData(new GatewayCommand
                {
                    Type = CommandType.LockKey,
                    Content = new LockKeyInfo
                    {
                        Key = key,
                        MicroServiceId = _GatewayConnector.ServiceId,
                        IsUnlock = true
                    }.ToJsonString()
                });

                var ret = netclient.ReadServiceObject<InvokeResult>();
            }
        }
    }
}
