using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using Microsoft.CodeAnalysis;
using System.Threading.Tasks;
using JMS.Cluster;

namespace JMS.Applications.CommandHandles
{
    class FindMasterHandler : ICommandHandler
    {
        IConfiguration _configuration;
        ClusterGatewayConnector _clusterGatewayConnector;

        public FindMasterHandler(IConfiguration configuration, ClusterGatewayConnector clusterGatewayConnector)
        {
            this._configuration = configuration;
            this._clusterGatewayConnector = clusterGatewayConnector;
        }
        public CommandType MatchCommandType => CommandType.FindMaster;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            if (cmd.IsHttp)
            {
                var contentBytes = Encoding.UTF8.GetBytes(new
                {
                    Success = _clusterGatewayConnector.IsMaster,
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
                    Success = _clusterGatewayConnector.IsMaster,
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
