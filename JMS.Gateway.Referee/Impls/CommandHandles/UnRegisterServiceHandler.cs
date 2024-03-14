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
    class UnRegisterServiceHandler : ICommandHandler
    {
        Referee _referee;
        ILogger<UnRegisterServiceHandler> _logger;
        public UnRegisterServiceHandler(IServiceProvider serviceProvider)
        {
            _referee = serviceProvider.GetService<Referee>();
            _logger = serviceProvider.GetService<ILogger<UnRegisterServiceHandler>>();
        }
        public CommandType MatchCommandType => CommandType.UnRegisterSerivce;

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

            _logger?.LogInformation($"{location.Host}:{location.Port} 服务断开");
            _referee.MasterGatewayServices.TryRemove($"{location.Host}:{location.Port}" , out RegisterServiceLocation original);

            netclient.WriteServiceData(new InvokeResult { 
                Success = true
            });
        }
    }
}
