using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace JMS.Proxy
{
    class Socks5Server
    {

        public IServiceProvider ServiceProvider { get; }
        ILogger<Socks5Server> _logger;
        public Socks5Server(IServiceProvider serviceProvider,ILogger<Socks5Server> logger)
        {
            this.ServiceProvider = serviceProvider;
            _logger = logger;
        }
        public void Run(int port)
        {
            var request = ServiceProvider.GetService<RequestHandler>();
            var _tcpListener = new TcpListener(IPAddress.Any, port);
            _tcpListener.Start();
            _logger?.LogInformation("Proxy started, port:{0}", port);
            while (true)
            {
                try
                {
                    var socket = _tcpListener.AcceptSocket();
                    Task.Run(() => request.Interview(socket));
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, ex.Message);
                    break;
                }

            }
        }
    }
}
