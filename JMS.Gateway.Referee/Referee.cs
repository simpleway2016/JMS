
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
using Microsoft.Extensions.DependencyInjection;
namespace JMS.Gateway
{
    class Referee
    {
        public NetAddress MasterIp { get; set; }
        internal ConcurrentDictionary<string,RegisterServiceLocation> MasterGatewayServices { get;  }
        ILogger<Referee> _logger;
        IServiceProvider _serviceProvider;
        public Referee(ILogger<Referee> logger,IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            MasterGatewayServices = new ConcurrentDictionary<string, RegisterServiceLocation>();
        }
        public void Run(int port)
        {
            var reception = _serviceProvider.GetService<IRequestReception>();
            var tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            _logger?.LogInformation("Gateway referee started, port:{0}", port);
            while (true)
            {
                try
                {
                    var socket = tcpListener.AcceptSocket();
                    Task.Run(() => reception.Interview(socket));
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
