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
using JMS.Dtos;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Linq;
using System.IO;
using Microsoft.Extensions.Configuration;
using JMS.Common.Net;
using Org.BouncyCastle.Bcpg;
using JMS.Cluster;

namespace JMS
{
    public class Gateway : IDisposable
    {
        JMS.ServerCore.MulitTcpListener _tcpServer;
        ILogger<Gateway> _Logger;
        private readonly IGatewayEnvironment _gatewayEnvironment;
        IRequestReception _requestReception;

    
        public IServiceProvider ServiceProvider { get; set; }

        internal bool Disposed { get; private set; }

        public Gateway(ILogger<Gateway> logger,IGatewayEnvironment gatewayEnvironment)
        {
            _Logger = logger;
            _gatewayEnvironment = gatewayEnvironment;
            _Logger.LogInformation($"版本号：{this.GetType().Assembly.GetName().Version}");

            _Logger?.LogInformation("配置文件:{0}", gatewayEnvironment.AppSettingPath);
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

        public void Run()
        {
           
            _requestReception = ServiceProvider.GetService<IRequestReception>();
            var sslConfiguration = ServiceProvider.GetService<ISslConfiguration>();

            _tcpServer = new JMS.ServerCore.MulitTcpListener(_gatewayEnvironment.Port, null);
            _tcpServer.Connected += _tcpServer_Connected;
            _tcpServer.OnError += _tcpServer_OnError;

            _Logger?.LogInformation("Gateway started, port:{0}", _gatewayEnvironment.Port);
            if (sslConfiguration.ServerCert != null)
            {
                _Logger?.LogInformation("Use ssl,certificate hash:{0}", sslConfiguration.ServerCert.GetCertHashString());
            }

            //启动GatewayRefereeClient，申请成为主网关
            ServiceProvider.GetService<ClusterGatewayConnector>().BeMaster();

            _tcpServer.Run();
        }

        private void _tcpServer_OnError(object sender, Exception e)
        {
            _Logger?.LogError(e , "");
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
