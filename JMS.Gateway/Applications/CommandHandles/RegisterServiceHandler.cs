using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using JMS.Domains;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
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

        public Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            string[] allowips = _configuration.GetSection("AllowIps").Get<string[]>();
            if(allowips != null && allowips.Length > 0 && allowips.Contains(((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString()) == false)
            {
                //不允许的ip
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false,
                    Error = "not allow"
                });
                return Task.CompletedTask;
            }

            var serviceClient = _serviceProvider.GetService<IMicroServiceReception>();
            return serviceClient.HealthyCheck(netclient , cmd);
        }
    }
}
