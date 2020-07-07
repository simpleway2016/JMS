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

namespace JMS.Impls.CommandHandles
{
    class UnRegisterServiceHandler : ICommandHandler
    {
        Referee _referee;
        public UnRegisterServiceHandler(IServiceProvider serviceProvider)
        {
            _referee = serviceProvider.GetService<Referee>();
        }
        public CommandType MatchCommandType => CommandType.UnRegisterSerivce;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var service = cmd.Content.FromJson<RegisterServiceInfo>();
            if (((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString() != _referee.MasterIp.Address)
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false
                });
                return;
            }

            _referee.MasterGatewayServices.TryRemove(service.ServiceId , out RegisterServiceInfo original);

            netclient.WriteServiceData(new InvokeResult { 
                Success = true
            });
        }
    }
}
