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
using System.Net;

namespace JMS.Applications.CommandHandles
{
    class GetAllServiceProvidersHandler : ICommandHandler
    {
        IConfiguration _configuration;
        IServiceProvider _serviceProvider;
        Gateway _Gateway;
        ErrorUserMarker _errorUserMarker;
        IRegisterServiceManager _RegisterServiceManager;
        public GetAllServiceProvidersHandler(IServiceProvider serviceProvider)
        {
            
            _serviceProvider = serviceProvider;
            _Gateway = serviceProvider.GetService<Gateway>();
            _RegisterServiceManager = serviceProvider.GetService<IRegisterServiceManager>();
            this._configuration = serviceProvider.GetService<IConfiguration>();
            _errorUserMarker = serviceProvider.GetService<ErrorUserMarker>();
        }
        public CommandType MatchCommandType => CommandType.GetAllServiceProviders;

        void outputNeedLogin(NetClient netclient,GatewayCommand cmd)
        {
            if (cmd.IsHttp)
            {
                netclient.OutputHttp401();
            }
            else
            {
                netclient.WriteServiceData(new RegisterServiceInfo[] { new RegisterServiceInfo() {
                        ServiceList = new ServiceDetail[]{ new ServiceDetail { Name = "username , password error" } }
                    } });
            }
        }
        void outputBlackList(NetClient netclient, GatewayCommand cmd)
        {
            if (cmd.IsHttp)
            {
                netclient.OutputHttp401();
            }
            else
            {
                netclient.WriteServiceData(new RegisterServiceInfo[] { new RegisterServiceInfo() {
                        ServiceList = new ServiceDetail[]{ new ServiceDetail { Name = "username , password error. in black list" } }
                    } });
            }
        }

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var userInfos = _configuration.GetSection("Http:Users").Get<UserInfo[]>();
            if(userInfos != null && userInfos.Length > 0)
            {
                var ip = ((IPEndPoint)netclient.RemoteEndPoint).Address.ToString();
                if(_errorUserMarker.CheckUserIp(ip) == false)
                {
                    outputBlackList(netclient,cmd);
                    return;
                }

                //检验身份
                string username, pwd;
                cmd.Header.TryGetValue("UserName", out username);
                if (string.IsNullOrWhiteSpace(username))
                {
                    outputNeedLogin(netclient, cmd);
                    return;
                }
                cmd.Header.TryGetValue("Password", out pwd);

                if(userInfos.Any(m=>string.Equals( m.UserName , username, StringComparison.OrdinalIgnoreCase) && m.Password == pwd) == false)
                {
                    _errorUserMarker.Error(ip);
                    outputNeedLogin(netclient, cmd);
                    return;
                }

                _errorUserMarker.Clear(ip);
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
