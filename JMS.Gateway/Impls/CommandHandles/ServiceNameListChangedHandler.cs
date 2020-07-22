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
    class ServiceNameListChangedHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _gateway;
        public ServiceNameListChangedHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _gateway = serviceProvider.GetService<Gateway>();
        }
        public CommandType MatchCommandType => CommandType.ServiceNameListChanged;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var info = cmd.Content.FromJson<RegisterServiceInfo>();
           var service =  _gateway.GetServiceById(info.ServiceId);
           if(service != null)
            {
                service.ServiceNames = info.ServiceNames;
            }

            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
