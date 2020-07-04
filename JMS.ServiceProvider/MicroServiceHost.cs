using JMS.GenerateCode;
using JMS.Impls;
using JMS.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    public class MicroServiceHost
    {
        ILogger<MicroServiceHost> _logger;
        IGatewayConnector _GatewayConnector;
        internal IGatewayConnector GatewayConnector => _GatewayConnector;
        public string GatewayAddress { private set; get; }
        public int GatewayPort { private set; get; }
        internal Dictionary<string, Type> ServiceNames = new Dictionary<string, Type>();
        internal int ServicePort;
        /// <summary>
        /// 当前处理中的请求数
        /// </summary>
        internal int ClientConnected;
        public IServiceProvider ServiceProvider { private set; get; }
        ServiceCollection _services;
        IRequestReception _RequestReception;
        public MicroServiceHost(ServiceCollection services)
        {
            _services = services;
            
        }

        internal void DisconnectGateway()
        {
            _GatewayConnector.DisconnectGateway();
        }


        /// <summary>
        /// 向网关注册服务
        /// </summary>
        /// <param name="gatewayAddress">网关地址</param>
        /// <param name="serviceName">服务名称</param>
        public void Register<T>(string serviceName) where T : MicroServiceControllerBase
        {
            _services.AddTransient<T>();
            ServiceNames[serviceName] = typeof(T);
        }

       
        public void Run(string gatewayAddress, int gatewayPort, int servicePort)
        {
            _services.AddSingleton<IKeyLocker, KeyLocker>();
            _services.AddSingleton<ICodeBuilder, CodeBuilder>();
            _services.AddSingleton<IGatewayConnector, GatewayConnector>();
            _services.AddSingleton<IRequestReception,RequestReception>();
            _services.AddSingleton<InvokeRequestHandler>();
            _services.AddSingleton<GenerateInvokeCodeRequestHandler>();
            _services.AddSingleton<CommitRequestHandler>();
            _services.AddSingleton<RollbackRequestHandler>();
            _services.AddSingleton<ProcessExitHandler>();
            _services.AddSingleton<MicroServiceHost>(this);
            _services.AddSingleton<TransactionDelegateCenter>();
            ServiceProvider = _services.BuildServiceProvider();

            _logger = ServiceProvider.GetService<ILogger<MicroServiceHost>>();
            _GatewayConnector = ServiceProvider.GetService<IGatewayConnector>();

            GatewayAddress = gatewayAddress;
            GatewayPort = gatewayPort;
            ServicePort = servicePort;

            _GatewayConnector.ConnectAsync();
            
            _RequestReception = ServiceProvider.GetService<IRequestReception>();
            TcpListener listener = new TcpListener(ServicePort);
            listener.Start();

            using (var processExitHandler = ServiceProvider.GetService<ProcessExitHandler>())
            {
                processExitHandler.Listen(this);

                while (true)
                {
                    var socket = listener.AcceptSocket();
                    if (processExitHandler.ProcessExited)
                        break;

                    Task.Run(() => _RequestReception.Interview(socket));
                }
            }
        }


      

    }
}
