using JMS.HttpProxy.Applications.Http;
using JMS.HttpProxy.AutoGenerateSslCert;
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
using Way.Lib;

namespace JMS.HttpProxy.Servers
{
    public class HttpServer : ProxyServer
    {
        HttpRequestReception _requestReception;
        JMS.ServerCore.MulitTcpListener _tcpServer;
        ILogger<HttpServer> _logger;

        X509Certificate2 _Certificate;
        public X509Certificate2 Certificate
        {
            get => _Certificate;

            set
            {
                if (_Certificate != value)
                {
                    var old = _Certificate;
                    _Certificate = value;
                    old?.Dispose();
                }
            }
        }
        bool _isAcme;

        ServerConfig _Config;
        public override ServerConfig Config
        {
            get => _Config;
            set
            {
                if (_Config.ToJsonString() != value.ToJsonString())
                {
                    _Config = value;
                    if (_isAcme)
                    {
                        var sslCertGenerator = this.ServiceProvider.GetService<SslCertGenerator>();
                        sslCertGenerator.RemoveRequest(this);
                        sslCertGenerator.AddRequest(this);
                    }
                }
            }
        }

        public HttpServer()
        {

        }

        public override void Init()
        {
            _logger = this.ServiceProvider.GetService<ILogger<HttpServer>>();
            _requestReception = ServiceProvider.GetService<HttpRequestReception>();
            _requestReception.SetServer(this);
            _tcpServer = new ServerCore.MulitTcpListener(this.Config.Port, null);
        }

        public override void Run()
        {
            _tcpServer.Connected += _tcpServer_Connected;
            _tcpServer.OnError += _tcpServer_OnError;
            if (Config.SSL != null)
            {
                if (Config.SSL.Acme != null)
                {
                    _isAcme = true;
                    var sslCertGenerator = this.ServiceProvider.GetService<SslCertGenerator>();
                    sslCertGenerator.AddRequest(this);
                }
                else
                {
                    this.Certificate = Config.SSL.Certificate;
                }
            }

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

            if (_isAcme)
            {
                _isAcme = false;
                var sslCertGenerator = this.ServiceProvider.GetService<SslCertGenerator>();
                sslCertGenerator.RemoveRequest(this);
            }
        }
    }
}
