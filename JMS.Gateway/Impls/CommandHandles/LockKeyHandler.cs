using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;

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

        public void Handle(NetStream netclient, GatewayCommand cmd)
        {
            var info = cmd.Content.FromJson<LockKeyInfo>();
            var service = _gateway.GetServiceById(info.MicroServiceId);
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
                    Success = true
                });
            }
            else if (!info.WaitToSuccess)
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false
                });
            }
            else
            {
                while (!_lockKeyManager.TryLock(info.Key, service))
                    Thread.Sleep(100);
            }
        }
    }
}
