using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class UnRegisterServiceHandler : ICommandHandler
    {
        IConfiguration _configuration;
        IRegisterServiceManager _registerServiceManager;

        public UnRegisterServiceHandler(IConfiguration configuration, IRegisterServiceManager registerServiceManager)
        {
            this._configuration = configuration;
            this._registerServiceManager = registerServiceManager;
        }
        public CommandType MatchCommandType => CommandType.UnRegisterSerivce;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var serviceid = cmd.Content;

            _registerServiceManager.DisconnectService(serviceid);

            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
