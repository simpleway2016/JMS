using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;

namespace JMS.Impls.CommandHandles
{
    class LockKeyHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        LockKeyManager _lockKeyManager;
        Gateway _gateway;
        public LockKeyHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _lockKeyManager = serviceProvider.GetService<LockKeyManager>();
            _gateway = serviceProvider.GetService<Gateway>();
        }
        public CommandType MatchCommandType => CommandType.LockKey;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var info = cmd.Content.FromJson<LockKeyInfo>();
            if(info.IsUnlock && info.MicroServiceId == "$$$")
            {
                //强制释放
                _lockKeyManager.UnLock(info.Key, null);
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = true
                });
                return;
            }

            var service = _gateway.GetServiceById(info.MicroServiceId);
            if(service == null)
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false,
                    Data = $"找不到注册的服务{info.MicroServiceId}"
                });
                return;
            }

            try
            {
                if (info.IsUnlock)
                {
                    _lockKeyManager.UnLock(info.Key, service);
                    netclient.WriteServiceData(new InvokeResult
                    {
                        Success = true
                    });
                    return;
                }

                if (_lockKeyManager.TryLock(info.Key, service))
                {
                    netclient.WriteServiceData(new InvokeResult
                    {
                        Success = true,
                        Data = _lockKeyManager.KeyTimeout
                    });
                }
                else
                {
                    netclient.WriteServiceData(new InvokeResult
                    {
                        Success = false
                    });
                }
            }
            catch (Exception ex)
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false,
                    Data = ex.Message
                });
            }
           
        }
    }
}
