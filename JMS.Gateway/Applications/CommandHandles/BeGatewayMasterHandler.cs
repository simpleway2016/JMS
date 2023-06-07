using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class BeGatewayMasterHandler : ICommandHandler
    {
        ClusterGatewayConnector _clusterGatewayConnector;
        public CommandType MatchCommandType => CommandType.BeGatewayMaster;
        public BeGatewayMasterHandler(ClusterGatewayConnector clusterGatewayConnector)
        {
            this._clusterGatewayConnector = clusterGatewayConnector;
        }
        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            _clusterGatewayConnector.SomeoneWantToBeMaster(netclient, cmd);
        }
    }
}
