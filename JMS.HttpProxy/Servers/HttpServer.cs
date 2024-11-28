using JMS.HttpProxy.Applications.Http;
using JMS.HttpProxy.Attributes;
using JMS.HttpProxy.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Servers
{
    [ProxyType(ProxyType.Http)]
    public class HttpServer : ProxyServer
    {
        HttpRequestReception _requestReception;
        JMS.ServerCore.MulitTcpListener _tcpServer;
        ILogger<HttpServer> _logger;
        public X509Certificate2 Certificate{ get; private set; }
        public HttpServer()
        {

        }

        public override void Init()
        {
            _logger = this.ServiceProvider.GetService<ILogger<HttpServer>>();
            _requestReception = ServiceProvider.GetService<HttpRequestReception>();
            _requestReception.SetServer(this);
            _tcpServer = new ServerCore.MulitTcpListener(this.Config.Port , null);
        }

        public override void Run()
        {
            _tcpServer.Connected += _tcpServer_Connected;
            _tcpServer.OnError += _tcpServer_OnError;
            if (Config.SSL != null)
                this.Certificate = Config.SSL.Certificate;

            _logger?.LogInformation($"Listening {(this.Certificate != null ? "https" : "http")}://*:{Config.Port}");
           
            _tcpServer.Run();
        }

        private void _tcpServer_OnError(object sender, Exception err)
        {
            _logger.LogError(err, "");
        }

        private void _tcpServer_Connected(object sender, System.Net.Sockets.Socket socket)
        {
            _requestReception.Interview(socket);
        }

        public override void Dispose()
        {
            if (_tcpServer != null)
            {
                _tcpServer.Stop();
                _tcpServer = null;
            }
        }
    }
}
