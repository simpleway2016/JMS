using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class LockKeyHandler : ICommandHandler
    {
        LockKeyManager _lockKeyManager;
        Gateway _gateway;
        IRegisterServiceManager _registerServiceManager;
        public LockKeyHandler(LockKeyManager lockKeyManager, Gateway gateway, IRegisterServiceManager registerServiceManager)
        {
            this._lockKeyManager = lockKeyManager;
            this._gateway = gateway;
            this._registerServiceManager = registerServiceManager;
        }
        public CommandType MatchCommandType => CommandType.LockKey;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
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

            var service = _registerServiceManager.GetServiceById(info.MicroServiceId);
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
                    if (info.Key == null)
                    {
                        _lockKeyManager.UnLockServiceAllKey(service);
                        netclient.WriteServiceData(new InvokeResult
                        {
                            Success = true
                        });
                    }
                    else
                    {
                        netclient.WriteServiceData(new InvokeResult
                        {
                            Success = _lockKeyManager.UnLock(info.Key, service)
                        });
                    }
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
