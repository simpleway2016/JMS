using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using JMS.Common.Net;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Bcpg;

namespace JMS.Proxy
{
    class Socks5Server
    {
        public NetAddress[] WhiteList { get; set; }
        public IServiceProvider ServiceProvider { get; }
        ILogger<Socks5Server> _logger;
        RequestHandler _requestHandler;
        public Socks5Server(IServiceProvider serviceProvider,ILogger<Socks5Server> logger)
        {
            this.ServiceProvider = serviceProvider;
            _logger = logger;
        }
        public async Task RunAsync(int port)
        {
            _requestHandler = ServiceProvider.GetService<RequestHandler>();
            var _tcpListener = new JMS.ServerCore.MulitTcpListener(port,null);
            _tcpListener.Connected += _tcpListener_Connected;
             _logger?.LogInformation("Proxy started, port:{0}", port);
            await _tcpListener.RunAsync();

        }

        private void _tcpListener_Connected(object sender, Socket socket)
        {
            Task.Run(() => _requestHandler.Interview(socket));
        }
    }
}
