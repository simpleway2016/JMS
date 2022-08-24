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
    class RemoveLockKeyHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        LockKeyManager _lockKeyManager;
        Gateway _gateway;
        IRegisterServiceManager _registerServiceManager;
        public RemoveLockKeyHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _lockKeyManager = serviceProvider.GetService<LockKeyManager>();
            _gateway = serviceProvider.GetService<Gateway>();
            _registerServiceManager = serviceProvider.GetService<IRegisterServiceManager>();
        }
        public CommandType MatchCommandType => CommandType.RemoveLockKey;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var key = cmd.Content;
            _lockKeyManager.RemoveKey(key);

            netclient.WriteServiceData(new InvokeResult
            {
                Success = true
            });
        }
    }
}
