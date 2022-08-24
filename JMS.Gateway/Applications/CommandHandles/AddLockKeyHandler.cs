using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;

namespace JMS.Applications.CommandHandles
{
    class AddLockKeyHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        LockKeyManager _lockKeyManager;
        Gateway _gateway;
        IRegisterServiceManager _registerServiceManager;
        public AddLockKeyHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _lockKeyManager = serviceProvider.GetService<LockKeyManager>();
            _gateway = serviceProvider.GetService<Gateway>();
            _registerServiceManager = serviceProvider.GetService<IRegisterServiceManager>();
        }
        public CommandType MatchCommandType => CommandType.AddLockKey;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var keyObject = cmd.Content.FromJson<KeyObject>();
            _lockKeyManager.AddKey(keyObject.Key, keyObject.Locker);

            netclient.WriteServiceData(new InvokeResult
            {
                Success = true
            });
        }
    }
}
