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

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var info = cmd.Content.FromJson<RegisterServiceInfo>();
           var service = _RegisterServiceManager.GetServiceById(info.ServiceId);
           if(service != null)
            {
                service.ServiceNames = info.ServiceNames;
                service.Description = info.Description;
                service.ClientCheckCodeFile = info.ClientCheckCodeFile;
                service.GatewayProxy = info.GatewayProxy;
               
            }

            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
