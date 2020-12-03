using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;
using System.Threading.Tasks;

namespace JMS.Impls.CommandHandles
{
    class ServiceNameListChangedHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _gateway;
        IServiceProviderAllocator _ServiceProviderAllocator;
        public ServiceNameListChangedHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _gateway = serviceProvider.GetService<Gateway>();
            _ServiceProviderAllocator = serviceProvider.GetService<IServiceProviderAllocator>();
        }
        public CommandType MatchCommandType => CommandType.ServiceNameListChanged;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var info = cmd.Content.FromJson<RegisterServiceInfo>();
           var service =  _gateway.GetServiceById(info.ServiceId);
           if(service != null)
            {
                service.ServiceNames = info.ServiceNames;
                service.Description = info.Description;
                service.ClientCheckCode = info.ClientCheckCode;

                Task.Run(() => {
                    _ServiceProviderAllocator.ServiceInfoChanged(_gateway.GetAllServiceProviders());
                });
            }

            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
