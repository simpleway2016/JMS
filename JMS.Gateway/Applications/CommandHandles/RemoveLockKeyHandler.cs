using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;
using Org.BouncyCastle.Utilities.IO.Pem;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class RemoveLockKeyHandler : ICommandHandler
    {
        LockKeyManager _lockKeyManager;
        Gateway _gateway;
        IRegisterServiceManager _registerServiceManager;
        public RemoveLockKeyHandler(LockKeyManager lockKeyManager, Gateway gateway, IRegisterServiceManager registerServiceManager)
        {
            this._lockKeyManager = lockKeyManager;
            this._gateway = gateway;
            this._registerServiceManager = registerServiceManager;
        }
        public CommandType MatchCommandType => CommandType.RemoveLockKey;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            while (true)
            {               
                if (cmd.Type == CommandType.HealthyCheck)
                    break;
                else if (cmd.Type == CommandType.AddLockKey)
                {
                    var keyObject = cmd.Content.FromJson<KeyObject>();
                    _lockKeyManager.AddKey(keyObject.Key, keyObject.Locker);
                }
                else if (cmd.Type == CommandType.RemoveLockKey)
                {
                    _lockKeyManager.RemoveKey(cmd.Content);
                }
                cmd = await netclient.ReadServiceObjectAsync<GatewayCommand>();
            }

            netclient.WriteServiceData(new InvokeResult
            {
                Success = true
            });
        }
    }
}
