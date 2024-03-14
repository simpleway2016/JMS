using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class HealthyCheckHandler : ICommandHandler
    {
        Gateway _gateway;
        public CommandType MatchCommandType => CommandType.HealthyCheck;
        public HealthyCheckHandler(Gateway gateway)
        {
            this._gateway = gateway;
        }
        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            netclient.ReadTimeout = 30000;
            while (!_gateway.Disposed)
            {
                netclient.WriteServiceData(new InvokeResult { 
                    Success = true
                });
                await netclient.ReadServiceObjectAsync<GatewayCommand>();                
            }
        }
    }
}
