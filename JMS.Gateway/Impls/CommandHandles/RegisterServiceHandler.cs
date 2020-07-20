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

namespace JMS.Impls.CommandHandles
{
    class RegisterServiceHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        IConfiguration _configuration;

        public RegisterServiceHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _configuration = serviceProvider.GetService<IConfiguration>();
        }
        public CommandType MatchCommandType => CommandType.RegisterSerivce;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            string[] allowips = _configuration.GetSection("AllowIps").Get<string[]>();
            if(allowips != null && allowips.Length > 0 && allowips.Contains(((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString()) == false)
            {
                //不允许的ip
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false
                });
                return;
            }

            var serviceClient = _serviceProvider.GetService<IMicroServiceReception>();
            serviceClient.HealthyCheck(netclient , cmd);
        }
    }
}
