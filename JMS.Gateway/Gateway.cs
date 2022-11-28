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

namespace JMS
{
    public class Gateway : IDisposable
    {
        TcpListener _tcpListener;
        TcpListener _tcpListenerV6;
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
            _tcpListener = new TcpListener(IPAddress.Any, port);
            _tcpListener.Start();

            _tcpListenerV6 = new TcpListener(IPAddress.IPv6Any, port);
            _tcpListenerV6.Start();
            _Logger?.LogInformation("Gateway started, port:{0}", port);
            if (ServerCert != null)
            {
                _Logger?.LogInformation("Use ssl,certificate hash:{0}", ServerCert.GetCertHashString());
            }

            //启动GatewayRefereeClient，申请成为主网关
            ServiceProvider.GetService<ClusterGatewayConnector>().BeMaster();

            new Thread(listenIPV6).Start();

            while (true)
            {
                try
                {
                    var socket = _tcpListener.AcceptSocket();
#if DEBUG
                    //Console.WriteLine("new socket");
#endif
                    Task.Run(() => _requestReception.Interview(socket));
                }
                catch (Exception ex)
                {
                    _Logger?.LogError(ex, ex.Message);
                    break;
                }

            }
        }

        void listenIPV6()
        {
            while (true)
            {
                try
                {
                    var socket = _tcpListenerV6.AcceptSocket();
                    Task.Run(() => _requestReception.Interview(socket));
                }
                catch (Exception ex)
                {
                    _Logger?.LogError(ex, ex.Message);
                    break;
                }

            }
        }

        public void Dispose()
        {
            if (!Disposed)
            {
                Disposed = true;
                ServiceProvider.GetService<IRegisterServiceManager>().DisconnectAllServices();

                if (_tcpListener != null)
                {
                    _tcpListener.Stop();
                    _tcpListener = null;
                }
            }
        }
    }
}
