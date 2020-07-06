using JMS.Dtos;
using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
namespace JMS.Gateway.Impls.CommandHandles
{
    class ApplyToBeMasterHandler : ICommandHandler
    {
        public CommandType MatchCommandType => CommandType.ApplyToBeMaster;
        Referee _referee;
        IServiceProvider _serviceProvider;
        public ApplyToBeMasterHandler(IServiceProvider serviceProvider)
        {
           
            _serviceProvider = serviceProvider;
            _referee = serviceProvider.GetService<Referee>();
        }

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            bool success;
            lock (_referee)
            {
                if (_referee.MasterIp == null)
                {
                    _referee.MasterIp = ((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString();
                    success = true;                  
                }
                else
                {
                    success = false;
                }
            }

            netclient.WriteServiceData(new InvokeResult
            {
                Success = success,
                Data = success ? _referee.MasterGatewayServices : null
            });

            if (success)
            {
                _serviceProvider.GetService<MasterGatewayConnector>().Start(netclient);
            }
        }
    }
}
