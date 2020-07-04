using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;

namespace JMS.Impls.CommandHandles
{
    class RegisterHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        public RegisterHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public CommandType MatchCommandType => CommandType.Register;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var serviceClient = _serviceProvider.GetService<IMicroServiceReception>();
            serviceClient.HealthyCheck(netclient , cmd);
        }
    }
}
