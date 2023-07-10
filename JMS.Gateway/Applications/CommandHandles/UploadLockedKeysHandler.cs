using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class UploadLockedKeysHandler : ICommandHandler
    {
        LockKeyManager _lockKeyManager;
        IRegisterServiceManager _registerServiceManager;
        public UploadLockedKeysHandler(LockKeyManager lockKeyManager, IRegisterServiceManager registerServiceManager)
        {
            this._lockKeyManager = lockKeyManager;
            this._registerServiceManager = registerServiceManager;
        }
        public CommandType MatchCommandType => CommandType.UploadLockKeys;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var keys = cmd.Content.FromJson<string[]>();
            var service = _registerServiceManager.GetServiceById(cmd.Header["ServiceId"]);
            if (service != null && service.Host == ((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString())
            {
                List<string> failed = new List<string>();
                foreach (var key in keys)
                {
                    //为了防止服务端可能同时会有删除key的并发行为，这里就不进行添加操作了，只是尝试清空RemoveTime
                    if( _lockKeyManager.TryRelock(key, service) == false)
                    {
                        failed.Add(key);
                    }
                }
                SystemEventCenter.OnMicroServiceUploadLockedKeyCompleted(service);
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = true,
                    Data = failed.ToArray()
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
    }
}
