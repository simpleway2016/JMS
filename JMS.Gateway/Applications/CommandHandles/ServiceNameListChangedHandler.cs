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
        IRegisterServiceManager _registerServiceManager;
        IServiceProviderAllocator _serviceProviderAllocator;
        public ServiceNameListChangedHandler(IRegisterServiceManager registerServiceManager, IServiceProviderAllocator serviceProviderAllocator)
        {
            this._registerServiceManager = registerServiceManager;
            this._serviceProviderAllocator = serviceProviderAllocator;
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

            var service = _registerServiceManager.GetServiceById(serviceItem.ServiceId);
           if(service != null)
            {
                service.ServiceList = serviceItem.ServiceList;
                service.Port = serviceItem.Port;
                service.SingletonService = serviceItem.SingletonService;
                service.ClientCheckCodeFile = serviceItem.ClientCheckCodeFile;
                _registerServiceManager.RefreshServiceInfo(service);
            }

            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
