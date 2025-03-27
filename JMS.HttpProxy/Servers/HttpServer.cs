using JMS.HttpProxy.Applications.Http;
using JMS.HttpProxy.AutoGenerateSslCert;
using JMS.HttpProxy.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
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
                    var domain = _Config?.SSL?.Acme?.Domain;
                    var old = _Config;
                    _Config = value;
                    if (old != null)
                    {
                        var sslCertGenerator = this.ServiceProvider.GetService<SslCertGenerator>();
                        if (_isAcme)
                        {                         
                            sslCertGenerator.RemoveRequest(this, domain);
                            _isAcme = false;
                        }

                        if (!string.IsNullOrEmpty(Config?.SSL?.Acme?.Domain))
                        {
                            _isAcme = true;
                            sslCertGenerator.AddRequest(this);
                        }

                        if (!_isAcme)
                        {
                            this.Certificate = value?.SSL?.Certificate;
                        }
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
                if (!string.IsNullOrEmpty(Config?.SSL?.Acme?.Domain))
                {
                    _isAcme = true;
                    var sslCertGenerator = this.ServiceProvider.GetService<SslCertGenerator>();
                    sslCertGenerator.AddRequest(this);
                }
                else
                {
                    this.Certificate = Config?.SSL?.Certificate;
                }
            }
            try
            {
                if (this.Config.Proxies.Any(m => m.RootPath != null))
                {
                    _logger?.LogInformation($"开始读文件");
                    readDir(this.Config.Proxies.Where(m => m.RootPath != null).Select(m => m.RootPath).First());
                }
            }
            catch (Exception ex)
            {

                _logger?.LogError(ex, null);
            }
            _logger?.LogInformation($"Listening {(this.Certificate != null ? "https" : "http")}://*:{Config.Port}");

           
            _tcpServer.Run();
        }
        const int FileBufSize = 20480;
        void readDir(string folder)
        {
            var dir = Directory.GetDirectories(folder);
            foreach (var sub in dir)
            {
                readDir(sub);
            }

            var files = Directory.GetFiles(folder);
           
            foreach (var file in files)
            {
                _logger?.LogInformation(file);
                for (int i = 0; i < 5; i++)
                {
                    using (var fs = new System.IO.FileStream(file, System.IO.FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite))
                    {
                        var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(fs.Length, FileBufSize));
                        while (true)
                        {
                            int readed = fs.Read(buffer, 0, buffer.Length);
                            if (readed == 0)
                                break;
                        }
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
           
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
                var domain = _Config?.SSL?.Acme?.Domain;
                var sslCertGenerator = this.ServiceProvider.GetService<SslCertGenerator>();
                sslCertGenerator.RemoveRequest(this, domain);
            }
        }
    }
}
