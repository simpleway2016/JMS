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
        IServiceProvider _serviceProvider;
        public CommandType MatchCommandType => CommandType.BeGatewayMaster;
        public BeGatewayMasterHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            _serviceProvider.GetService<ClusterGatewayConnector>().SomeoneWantToBeMaster(netclient, cmd);
        }
    }
}
