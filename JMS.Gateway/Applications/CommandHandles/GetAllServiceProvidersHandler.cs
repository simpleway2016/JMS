using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using JMS.Dtos;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net;
using JMS.Authentication;

namespace JMS.Applications.CommandHandles
{
    class GetAllServiceProvidersHandler : ICommandHandler
    {
        IAuthentication _authentication;
        IRegisterServiceManager _registerServiceManager;
        IConfiguration _configuration;

        public GetAllServiceProvidersHandler(IRegisterServiceManager registerServiceManager, IConfiguration configuration,
            IAuthentication authentication)
        {
            this._authentication = authentication;
            this._registerServiceManager = registerServiceManager;
            this._configuration = configuration;

        }
        public CommandType MatchCommandType => CommandType.GetAllServiceProviders;

       

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            if (!(await _authentication.Verify(netclient, cmd)))
            {
                return;
            }

            var locations = this.List(cmd.Content);
            if (cmd.IsHttp)
            {
                if (_configuration.GetSection("Http:GetAllServiceProviders").Get<bool?>() != false)
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
                if (_configuration.GetSection("Http:GetAllServiceProviders").Get<bool?>() != false)
                {
                    netclient.WriteServiceData(locations);
                }
                else
                {
                    netclient.WriteServiceData(new RegisterServiceInfo[] { new RegisterServiceInfo() { 
                        ServiceList = new ServiceDetail[]{ new ServiceDetail { Name = "Http:GetAllServiceProviders 未开启" } }
                    } });
                }
            }
        }

        public RegisterServiceRunningInfo[] List(string serviceName)
        {
            var list = _registerServiceManager.GetAllRegisterServices();
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
                MaxRequestCount = m.MaxRequestCount,
                UseSsl = m.UseSsl,
                Properties = m.Properties,
                PerformanceInfo = new PerformanceInfo
                {
                    RequestQuantity = m.RequestQuantity,
                    CpuUsage = m.CpuUsage
                }
            }).ToArray();
        }
    }
}
