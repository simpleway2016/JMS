using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using JMS.Dtos;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace JMS.Applications.CommandHandles
{
    class GetAllServiceProvidersHandler : ICommandHandler
    {
        IConfiguration _configuration;
        IServiceProvider _serviceProvider;
        Gateway _Gateway;
        IRegisterServiceManager _RegisterServiceManager;
        public GetAllServiceProvidersHandler(IServiceProvider serviceProvider)
        {
            
            _serviceProvider = serviceProvider;
            _Gateway = serviceProvider.GetService<Gateway>();
            _RegisterServiceManager = serviceProvider.GetService<IRegisterServiceManager>();
            this._configuration = serviceProvider.GetService<IConfiguration>();
        }
        public CommandType MatchCommandType => CommandType.GetAllServiceProviders;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var locations = this.List(cmd.Content);
            if (cmd.IsHttp)
            {
                if (_configuration.GetSection("Http:GetAllServiceProviders").Get<bool>())
                {
                    var contentBytes = Encoding.UTF8.GetBytes(locations.ToJsonString());
                    netclient.OutputHttpContent(contentBytes);
                }
                else
                {
                    netclient.OutputHttpNotFund();
                }
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
                list = list.Where(m => m.ServiceList.Any(m=>m.Name == serviceName));
            }

            return list.Select(m => new RegisterServiceRunningInfo
            {
                Host = m.Host,
                ServiceAddress = m.ServiceAddress,
                Port = m.Port,
                ServiceId = m.ServiceId,
                ServiceList = m.ServiceList,
                MaxThread = m.MaxThread,
                UseSsl = m.UseSsl,
                PerformanceInfo = new PerformanceInfo
                {
                    RequestQuantity = m.RequestQuantity,
                    CpuUsage = m.CpuUsage
                }
            }).ToArray();
        }
    }
}
