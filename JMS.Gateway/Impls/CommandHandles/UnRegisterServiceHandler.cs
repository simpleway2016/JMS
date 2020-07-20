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
    class UnRegisterServiceHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        IConfiguration _configuration;
        Gateway _gateway;

        public UnRegisterServiceHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _configuration = serviceProvider.GetService<IConfiguration>();
            _gateway = serviceProvider.GetService<Gateway>();
        }
        public CommandType MatchCommandType => CommandType.UnRegisterSerivce;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var serviceid = cmd.Content;

           for(int i = 0; i < _gateway.OnlineMicroServices.Count; i ++)
            {
                try
                {
                    if (_gateway.OnlineMicroServices[i].ServiceInfo.ServiceId == serviceid)
                    {
                        _gateway.OnlineMicroServices[i].Close();
                        break;
                    }
                }
                catch 
                {
 
                }
            }

            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
