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
using Microsoft.Extensions.Logging;

namespace JMS.Impls.CommandHandles
{
    class RegisterServiceHandler : ICommandHandler
    {
        Referee _referee;
        ILogger<RegisterServiceHandler> _logger;
        public RegisterServiceHandler(IServiceProvider serviceProvider)
        {
            _referee = serviceProvider.GetService<Referee>();
            _logger = serviceProvider.GetService<ILogger<RegisterServiceHandler>>();
        }
        public CommandType MatchCommandType => CommandType.RegisterSerivce;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var location = cmd.Content.FromJson<RegisterServiceLocation>();
            if (((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString() != _referee.MasterIp.Address)
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false
                });
                return;
            }

            _logger?.LogInformation($"{location.Host}:{location.Port} 服务注册");
            _referee.MasterGatewayServices[$"{location.Host}:{location.Port}"] = location;

            netclient.WriteServiceData(new InvokeResult { 
                Success = true
            });
        }
    }
}
