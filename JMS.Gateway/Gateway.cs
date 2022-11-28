using JMS.Applications;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using JMS.Domains;
using JMS.Dtos;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Linq;
using System.IO;
using Microsoft.Extensions.Configuration;
using JMS.Common.Net;
using Org.BouncyCastle.Bcpg;

namespace JMS
{
    public class Gateway : IDisposable
    {
        TcpServer _tcpServer;
        ILogger<Gateway> _Logger;
        IRequestReception _requestReception;
        internal int Port;
        public X509Certificate2 ServerCert { get; set; }
        public string[] AcceptCertHash { get; set; }
        internal IServiceProvider ServiceProvider { get; set; }

        internal bool Disposed { get; private set; }

        public Gateway(ILogger<Gateway> logger)
        {
            _Logger = logger;
            _Logger.LogInformation($"版本号：{this.GetType().Assembly.GetName().Version}");
        }

        string _id;
        public string Id
        {
            get
            {
                if (_id == null)
                {
                    var configuration = ServiceProvider.GetService<IConfiguration>();
                    var datafolder = configuration.GetValue<string>("DataFolder");
                    var file = $"{datafolder}/GatewayId.txt";
                    if (File.Exists(file))
                    {
                        _id = File.ReadAllText(file, Encoding.UTF8);
                    }
                    else
                    {
                        _id = Guid.NewGuid().ToString("N");
                        File.WriteAllText(file, _id, Encoding.UTF8);
                    }
                }
                return _id;
            }
        }

        public void Run(int port)
        {
            NatashaInitializer.InitializeAndPreheating();
            _Logger?.LogInformation("初始化动态代码引擎");

            this.Port = port;
            _requestReception = ServiceProvider.GetService<IRequestReception>();
            _tcpServer = new TcpServer(port);
            _tcpServer.Connected += _tcpServer_Connected;

            _Logger?.LogInformation("Gateway started, port:{0}", port);
            if (ServerCert != null)
            {
                _Logger?.LogInformation("Use ssl,certificate hash:{0}", ServerCert.GetCertHashString());
            }

            //启动GatewayRefereeClient，申请成为主网关
            ServiceProvider.GetService<ClusterGatewayConnector>().BeMaster();

            _tcpServer.Run();
        }

        private void _tcpServer_Connected(object sender, Socket socket)
        {
            Task.Run(() => _requestReception.Interview(socket));
        }

        public void Dispose()
        {
            if (!Disposed)
            {
                Disposed = true;
                ServiceProvider.GetService<IRegisterServiceManager>().DisconnectAllServices();

                if (_tcpServer != null)
                {
                    _tcpServer.Stop();
                    _tcpServer = null;
                }
            }
        }
    }
}
