using JMS.Common.Dtos;
using JMS.Dtos;
using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Gateway
{
    class Referee
    {
        public NetAddress MasterIp { get; set; }
        internal ConcurrentDictionary<string,RegisterServiceInfo> MasterGatewayServices { get; }
        ILogger<Referee> _logger;
        IRequestReception _requestReception;
        public Referee(ILogger<Referee> logger,IRequestReception requestReception)
        {
            _logger = logger;
            _requestReception = requestReception;
            MasterGatewayServices = new ConcurrentDictionary<string, RegisterServiceInfo>();
        }
        public void Run(int port)
        {
            var tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            _logger?.LogInformation("Gateway referee started, port:{0}", port);
            while (true)
            {
                try
                {
                    var socket = tcpListener.AcceptSocket();
                    Task.Run(() => _requestReception.Interview(socket));
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                    break;
                }

            }
        }
    }
}
