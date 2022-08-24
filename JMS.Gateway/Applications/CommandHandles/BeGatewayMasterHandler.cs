using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

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
        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            _serviceProvider.GetService<ClusterGatewayConnector>().SomeoneWantToBeMaster(netclient, cmd);
        }
    }
}
