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
    class ServiceNameListChangedHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _gateway;
        IServiceProviderAllocator _ServiceProviderAllocator;
        IRegisterServiceManager _RegisterServiceManager;
        public ServiceNameListChangedHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _gateway = serviceProvider.GetService<Gateway>();
            _RegisterServiceManager = serviceProvider.GetService<IRegisterServiceManager>();
            _ServiceProviderAllocator = serviceProvider.GetService<IServiceProviderAllocator>();
        }
        public CommandType MatchCommandType => CommandType.ServiceNameListChanged;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var serviceItem = cmd.Content.FromJson<RegisterServiceInfo>();
            if (serviceItem.ServiceList == null)
            {
                serviceItem.ServiceList = new ServiceDetail[serviceItem.ServiceNames.Length];
                for (int i = 0; i < serviceItem.ServiceList.Length; i++)
                {
                    var detail = new ServiceDetail
                    {
                        Name = serviceItem.ServiceNames[i],
                        Type = ServiceType.JmsService
                    };
                    if (serviceItem.Port == 0)
                    {
                        detail.Type = ServiceType.WebApi;
                        detail.AllowGatewayProxy = true;
                    }
                }
            }

            var service = _RegisterServiceManager.GetServiceById(serviceItem.ServiceId);
           if(service != null)
            {
                service.ServiceList = serviceItem.ServiceList;
                service.ClientCheckCodeFile = serviceItem.ClientCheckCodeFile;
               
            }

            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
