using JMS.Dtos;
using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
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
        public void Interview(Socket socket)
        {
            try
            {
                using (var client = new NetClient(socket))
                {
                    var cmd = client.ReadServiceObject<GatewayCommand>();
                    _logger?.LogDebug("收到命令，type:{0} content:{1}", cmd.Type, cmd.Content);

                    _manager.AllocHandler(cmd)?.Handle(client, cmd);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
            }

           
        }
    }
}
