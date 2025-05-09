using JMS.HttpProxy.Applications.InternalProtocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Servers
{
    public class InternalProtocolServer : ProxyServer
    {
        JMS.ServerCore.MulitTcpListener _tcpServer;
        ProtocolRequestReception _requestReception;
        ILogger<InternalProtocolServer> _logger;
        public override void Dispose()
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
            _requestReception = this.ServiceProvider.GetService<ProtocolRequestReception>();
            _requestReception.SetServer(this);

            _logger = this.ServiceProvider.GetService<ILogger<InternalProtocolServer>>();
        }

        public override async Task RunAsync()
        {
            _tcpServer.Connected += _tcpServer_Connected;
            _tcpServer.OnError += _tcpServer_OnError;

            _logger?.LogInformation($"Listening internal protocol prot:{Config.Port}");

            await _tcpServer.RunAsync();
        }

        private void _tcpServer_OnError(object sender, Exception err)
        {
            _logger.LogError(err, "");
        }

        private void _tcpServer_Connected(object sender, System.Net.Sockets.Socket socket)
        {
            _requestReception.Interview(socket);
        }

    }
}
