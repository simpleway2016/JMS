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
        IRegisterServiceManager _registerServiceManager;

        public UnRegisterServiceHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _configuration = serviceProvider.GetService<IConfiguration>();
            _registerServiceManager = serviceProvider.GetService<IRegisterServiceManager>();
        }
        public CommandType MatchCommandType => CommandType.UnRegisterSerivce;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var serviceid = cmd.Content;

            _registerServiceManager.DisconnectService(serviceid);

            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
