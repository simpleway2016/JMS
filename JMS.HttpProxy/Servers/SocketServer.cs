using JMS.HttpProxy.Applications.InternalProtocol;
using JMS.HttpProxy.Applications.Sockets;
using JMS.HttpProxy.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Servers
{
    public class SocketServer : ProxyServer,IDisposable
    {
        JMS.ServerCore.MulitTcpListener _tcpServer;
        SocketRequestReception _requestReception;
        ILogger<SocketServer> _logger;
        public void Dispose()
        {
            if (_tcpServer != null)
            {
                _tcpServer.Stop();
                _tcpServer = null;
            }
        }

        public override void Init()
        {
            _tcpServer = new ServerCore.MulitTcpListener(Config.Port, null);
            _requestReception = this.ServiceProvider.GetService<SocketRequestReception>();
            _requestReception.SetServer(this);

            _logger = this.ServiceProvider.GetService<ILogger<SocketServer>>();
        }

        public override void Run()
        {
            _tcpServer.Connected += _tcpServer_Connected;
            _tcpServer.OnError += _tcpServer_OnError;

            _logger?.LogInformation($"SocketServer:{Config.Port} {this.Config.Proxies.FirstOrDefault().ToJsonString()}");

            _tcpServer.Run();
        }

        private void _tcpServer_OnError(object sender, Exception err)
        {
            _logger.LogError(err, "");
        }

        private void _tcpServer_Connected(object sender, System.Net.Sockets.Socket socket)
        {
            Task.Run(() => _requestReception.Interview(socket));
        }

    }
}
