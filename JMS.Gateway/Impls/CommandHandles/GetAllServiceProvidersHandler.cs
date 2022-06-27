using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using JMS.Dtos;
using System.Threading.Tasks;

namespace JMS.Impls.CommandHandles
{
    class GetAllServiceProvidersHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _Gateway;
        IRegisterServiceManager _RegisterServiceManager;
        public GetAllServiceProvidersHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _Gateway = serviceProvider.GetService<Gateway>();
            _RegisterServiceManager = serviceProvider.GetService<IRegisterServiceManager>();
        }
        public CommandType MatchCommandType => CommandType.GetAllServiceProviders;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var locations = this.List(cmd.Content);
            if (cmd.IsHttp)
            {
                var contentBytes = Encoding.UTF8.GetBytes(locations.ToJsonString());               
                netclient.OutputHttpContent(contentBytes);
            }
            else
            {
                netclient.WriteServiceData(locations);
            }
        }

        public RegisterServiceRunningInfo[] List(string serviceName)
        {
            var list = _RegisterServiceManager.GetAllRegisterServices();
            if (!string.IsNullOrEmpty(serviceName))
            {
                list = list.Where(m => m.ServiceNames.Contains(serviceName));
            }

            return list.Select(m => new RegisterServiceRunningInfo
            {
                Host = m.Host,
                ServiceAddress = m.ServiceAddress,
                Port = m.Port,
                ServiceId = m.ServiceId,
                ServiceNames = m.ServiceNames,
                Description = m.Description,
                MaxThread = m.MaxThread,
                PerformanceInfo = new PerformanceInfo
                {
                    RequestQuantity = m.RequestQuantity,
                    CpuUsage = m.CpuUsage
                }
            }).ToArray();
        }
    }
}
