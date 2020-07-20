using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using JMS.Gateway;
using System.Collections.Concurrent;

namespace JMS.Impls.CommandHandles
{
    class SetAllServicesHandler : ICommandHandler
    {
        Referee _referee;
        public SetAllServicesHandler(IServiceProvider serviceProvider)
        {
            _referee = serviceProvider.GetService<Referee>();
        }
        public CommandType MatchCommandType => CommandType.SetAllServices;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var arr = cmd.Content.FromJson<RegisterServiceLocation[]>();
            if (((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString() != _referee.MasterIp.Address)
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false
                });
                return;
            }

            foreach( var location in arr)
            {
                _referee.MasterGatewayServices[$"{location.Host}:{location.Port}"] = location;
            }            

            netclient.WriteServiceData(new InvokeResult { 
                Success = true
            });
        }
    }
}
