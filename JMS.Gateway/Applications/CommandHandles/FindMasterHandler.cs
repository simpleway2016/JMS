using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using Microsoft.CodeAnalysis;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class FindMasterHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        IConfiguration _configuration;
        ClusterGatewayConnector _gatewayRefereeClient;

        public FindMasterHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _configuration = serviceProvider.GetService<IConfiguration>();
            _gatewayRefereeClient = serviceProvider.GetService<ClusterGatewayConnector>();
        }
        public CommandType MatchCommandType => CommandType.FindMaster;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            if (cmd.IsHttp)
            {
                var contentBytes = Encoding.UTF8.GetBytes(new
                {
                    Success = _gatewayRefereeClient.IsMaster,
                    Data = new
                    {
                        SupportRetmoteClientConnect = true,
                        Version = this.GetType().Assembly.GetName().Version.ToString()
                    }
                }.ToJsonString());
                netclient.OutputHttpContent(contentBytes);
            }
            else
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = _gatewayRefereeClient.IsMaster,
                    Data = new
                    {
                        SupportRetmoteClientConnect = true,
                        Version = this.GetType().Assembly.GetName().Version.ToString()
                    }
                });
            }

        }
    }
}
