using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace JMS.Impls.CommandHandles
{
    class GetAllServiceProvidersHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _Gateway;
        public GetAllServiceProvidersHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _Gateway = serviceProvider.GetService<Gateway>();
        }
        public CommandType MatchCommandType => CommandType.GetAllServiceProviders;

        public void Handle(NetStream netclient, GatewayCommand cmd)
        {
            var locations = this.List(cmd.Content);
            netclient.WriteServiceData(locations);

        }

        public RegisterServiceLocation[] List(string serviceName)
        {
            if (serviceName == "")
            {
                var allocator = _serviceProvider.GetService<IServiceProviderAllocator>();
                return _Gateway.GetAllServiceProviders().Select(m => new RegisterServiceRunningInfo
                {
                    Host = m.Host,
                    Port = m.Port,
                    ServiceNames = m.ServiceNames,
                    MaxThread = m.MaxThread,
                    ClientConnected = allocator.GetClientConnectQuantity(m)
                }).ToArray();
            }
            else
            {
                return _Gateway.GetAllServiceProviders().Select(m => new RegisterServiceLocation
                {
                    Host = m.Host,
                    Port = m.Port
                }).ToArray();
            }
        }
    }
}
