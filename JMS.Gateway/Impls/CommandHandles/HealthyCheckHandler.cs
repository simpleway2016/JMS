using JMS.Dtos;
using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Impls.CommandHandles
{
    class HealthyCheckHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        public CommandType MatchCommandType => CommandType.HealthyCheck;
        public HealthyCheckHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            netclient.ReadTimeout = 30000;
            while (true)
            {
                netclient.WriteServiceData(new InvokeResult { 
                    Success = true
                });
                netclient.ReadServiceObject<GatewayCommand>();                
            }
        }
    }
}
