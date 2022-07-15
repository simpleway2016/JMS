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

namespace JMS
{
    class Gateway
    {
        TcpListener _tcpListener;
        ILogger<Gateway> _Logger;
        IRequestReception _requestReception;
        internal int Port;
        public X509Certificate2 ServerCert { get; set; }
        public string[] AcceptCertHash { get; set; }
        internal IServiceProvider ServiceProvider { get; set; }

        public Gateway(ILogger<Gateway> logger)
        {
            _Logger = logger;
            _Logger.LogInformation($"版本号：{this.GetType().Assembly.GetName().Version}");
        }


        public void Run(int port)
        {
            NatashaInitializer.InitializeAndPreheating();
            _Logger?.LogInformation("初始化动态代码引擎");

            this.Port = port;
            _requestReception = ServiceProvider.GetService<IRequestReception>();
               _tcpListener = new TcpListener(IPAddress.Any, port);
            _tcpListener.Start();
            _Logger?.LogInformation("Gateway started, port:{0}", port );
            if(ServerCert != null)
            {
                _Logger?.LogInformation("Use ssl,certificate hash:{0}", ServerCert.GetCertHashString());
            }
            while (true)
            {
                try
                {
                    var socket = _tcpListener.AcceptSocket();
                    Task.Run(() => _requestReception.Interview(socket));
                }
                catch (Exception ex)
                {
                    _Logger?.LogError(ex, ex.Message);
                    break;
                }
               
            }
        }

    }
}
