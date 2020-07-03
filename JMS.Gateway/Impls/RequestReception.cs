using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS.Impls
{
    class RequestReception : IRequestReception
    {
        ILogger<RequestReception> _logger;
        ICommandHandlerManager _manager;
        public RequestReception(ILogger<RequestReception> logger, ICommandHandlerManager manager)
        {
            _logger = logger;
            _manager = manager;
        }
        public void Interview(NetStream netclient)
        {
            var cmd = netclient.ReadServiceObject<GatewayCommand>();
            _logger?.LogDebug("收到命令，type:{0} content:{1}", cmd.Type, cmd.Content);

            _manager.AllocHandler(cmd)?.Handle(netclient, cmd);
        }
    }
}
