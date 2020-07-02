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
    public class MicroServiceProvider
    {
        ILogger<MicroServiceProvider> _logger;
        GatewayConnector _GatewayConnector;
        string _gatewayAddress;
        int _gatewayPort;
        internal Dictionary<string, Type> ServiceNames = new Dictionary<string, Type>();
        int _servicePort;
        /// <summary>
        /// 当前处理中的请求数
        /// </summary>
        internal int ClientConnected;
        public IServiceProvider ServiceProvider { private set; get; }
        ServiceCollection _services;
        public MicroServiceProvider(ServiceCollection services)
        {
            _services = services;
            
        }

        private void _GatewayConnector_Disconnect(object sender, EventArgs e)
        {
            Thread.Sleep(2000);
            try
            {
                _GatewayConnector.Connect(_gatewayAddress, _gatewayPort);
                _GatewayConnector.Register(ServiceNames, _servicePort);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
            }
        }

        /// <summary>
        /// 向网关注册服务
        /// </summary>
        /// <param name="gatewayAddress">网关地址</param>
        /// <param name="serviceName">服务名称</param>
        public void Register<T>(string serviceName) where T : MicroServiceController
        {
            _services.AddTransient<T>();
            ServiceNames[serviceName] = typeof(T);
        }

       
        public void Run(string gatewayAddress, int gatewayPort, int servicePort)
        {
            _services.AddSingleton<ProcessExitHandler>();
            _services.AddSingleton<TransactionDelegateCenter>();
            ServiceProvider = _services.BuildServiceProvider();

            _GatewayConnector = new GatewayConnector(ServiceProvider.GetService<ILogger<GatewayConnector>>() , this);
            _logger = ServiceProvider.GetService<ILogger<MicroServiceProvider>>();
            _GatewayConnector.Disconnect += _GatewayConnector_Disconnect;

            _gatewayAddress = gatewayAddress;
            _gatewayPort = gatewayPort;
            _servicePort = servicePort;
            _GatewayConnector.Connect(gatewayAddress, gatewayPort);
            _GatewayConnector.Register(ServiceNames, _servicePort);

            TcpListener listener = new TcpListener(_servicePort);
            listener.Start();

            using (var processExitHandler = ServiceProvider.GetService<ProcessExitHandler>())
            {
                processExitHandler.Listen(this);

                while (true)
                {
                    var socket = listener.AcceptSocket();
                    if (processExitHandler.ProcessExited)
                        break;

                    Task.Run(() => onSocketConnect(socket));
                }
            }
        }


        void onSocketConnect(Socket socket)
        {
            try
            {
                Interlocked.Increment(ref ClientConnected);
                new ClientHandler(this).Handle(socket);
            }
            catch(SocketException)
            {

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref ClientConnected);
            }
        }

    }
}
