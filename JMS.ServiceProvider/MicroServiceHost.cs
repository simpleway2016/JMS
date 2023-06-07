using JMS.GenerateCode;
using JMS.Applications;
using JMS.Domains;
using JMS.ScheduleTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;
using System.Reflection;

using System.Runtime.InteropServices;
using JMS.Infrastructures.Hardware;
using JMS.Infrastructures.Haredware;
using System.Security.Cryptography.X509Certificates;
using JMS.RetryCommit;
using JMS.Applications;
using JMS.Domains;
using JMS.Infrastructures;
using System.Collections.Concurrent;
using JMS.Common.Net;
using Org.BouncyCastle.Bcpg;
using JMS.Dtos;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JMS
{
    public class MicroServiceHost : IMicroServiceOption, IDisposable
    {
        bool _disposed;
        JMS.ServerCore.MulitTcpListener _tcpServer;
        public string Id { get; private set; }
        ILogger<MicroServiceHost> _logger;
        IGatewayConnector _GatewayConnector;
        ControllerFactory _ControllerFactory;
        IProcessExitHandler _processExitHandler;
        internal IGatewayConnector GatewayConnector => _GatewayConnector;
        public NetAddress MasterGatewayAddress { internal set; get; }
        public NetAddress[] AllGatewayAddresses { get; private set; }

       

        public IServiceProvider ServiceProvider { private set; get; }
        /// <summary>
        /// 设置微服务的地址，如果为null，网关会使用微服务的外网ip作为服务地址
        /// </summary>
        public NetAddress ServiceAddress { get; set; }
        public int ServicePort { get; private set; }


        private string _Description;
        /// <summary>
        /// 自定义描述
        /// </summary>
        public string Description
        {
            get => _Description;
            set
            {
                if (_Description != value)
                {
                    _Description = value;
                    _GatewayConnector?.OnServiceInfoChanged();
                }
            }
        }

        /// <summary>
        /// 最多允许多少个请求数。默认值为0，表示无限制。
        /// </summary>
        public int MaxRequestCount
        {
            get;
            set;
        }

        private string _ClientCheckCodeFile;
        /// <summary>
        /// 自定义客户端检验代码在网关服务器上的文件位置
        /// </summary>
        public string ClientCheckCodeFile
        {
            get => _ClientCheckCodeFile;
            set
            {
                if (_ClientCheckCodeFile != value)
                {
                    _ClientCheckCodeFile = value;
                    _GatewayConnector?.OnServiceInfoChanged();
                }
            }
        }

        /// <summary>
        /// 当与网关连接断开时，是否自动关闭进程
        /// 在双机热备的情况下，可以考虑设置此属性为true，因为与网关连接断开后，可能继续运行业务，会与后面启动的备份服务产生冲突
        /// </summary>
        public bool AutoExitProcess
        {
            get;
            set;
        }

        string _RetryCommitPath = "./$$JMS_RetryCommitPath";
        /// <summary>
        /// 当提交事务失败后，保存请求数据到哪个目录，默认./$$JMS_RetryCommitPath
        /// </summary>
        public string RetryCommitPath
        {
            get => _RetryCommitPath;
            set
            {
                if (_RetryCommitPath != value)
                {
                    _RetryCommitPath = value;
                }
            }
        }

        /// <summary>
        /// 是否同一时间只有一个相同的服务器运行（双机热备）
        /// 当此属性设为true，如果与网关连接断开，会自动退出进程
        /// </summary>
        public bool SingletonService
        {
            get;
            set;
        }
        /// <summary>
        /// 依赖注入容器就绪事件
        /// </summary>
        public event EventHandler<IServiceProvider> ServiceProviderBuilded;

        internal IServiceCollection _services;
        IRequestReception _RequestReception;
        ScheduleTaskManager _scheduleTaskManager;


        public MicroServiceHost(IServiceCollection services)
        {
            this.Id = Guid.NewGuid().ToString("N");
            _services = services;
            _scheduleTaskManager = new ScheduleTaskManager(this);
            _ControllerFactory = new ControllerFactory(this);

            registerServices();
        }

        internal void DisconnectGateway()
        {
            _GatewayConnector.DisconnectGateway();
        }


        /// <summary>
        /// 向网关注册服务
        /// </summary>
        /// <typeparam name="T">Controller</typeparam>
        /// <param name="serviceName">服务名称</param>
        /// <param name="allowGatewayProxy">允许通过网关反向代理</param>
        public void Register<T>(string serviceName,  bool? allowGatewayProxy = null) where T : BaseJmsController
        {
            this.Register(typeof(T), serviceName,null,allowGatewayProxy);
        }

        /// <summary>
        /// 向网关注册服务
        /// </summary>
        /// <typeparam name="T">Controller</typeparam>
        /// <param name="serviceName">服务名称</param>
        /// <param name="description">服务描述</param>
        /// <param name="allowGatewayProxy">允许通过网关反向代理</param>
        public void Register<T>(string serviceName,string description, bool? allowGatewayProxy = null) where T : BaseJmsController
        {
            this.Register(typeof(T), serviceName, description, allowGatewayProxy);
        }

        bool _isWebServer;
        /// <summary>
        /// 把当前程序注册为一个web服务器，并且Run时不再启动指定的网络端口
        /// </summary>
        /// <param name="webServerUrl">web服务器的根访问路径，如 http://192.168.2.128:8080 </param>
        /// <param name="serverName">服务名称，默认为WebServer</param>
        /// <param name="description">服务描述</param>
        /// <param name="allowGatewayProxy">允许通过网关反向代理</param>
        public void RegisterWebServer(string webServerUrl, string serverName = "WebServer",string description = null, bool? allowGatewayProxy = null)
        {
            _isWebServer = true;
            this.ServiceAddress = new NetAddress(webServerUrl, 0);
            var serviceDetail = new ServiceDetail()
            {
                Name = serverName,
                Description = description,
                Type = ServiceType.WebApi,
                AllowGatewayProxy = allowGatewayProxy??true,
            };
            _ControllerFactory.RegisterWebServer(serviceDetail);
        }

        /// <summary>
        /// 向网关注册服务
        /// </summary>
        /// <param name="controllerType">Controller类型</param>
        /// <param name="description">服务描述</param>
        /// <param name="serviceName">服务名称</param>
        public void Register(Type controllerType, string serviceName , string description, bool? allowGatewayProxy = null)
        {
            if (_ControllerFactory.GetAllControllers().Any(m => m.Service.Name == serviceName))
            {
                throw new Exception("服务名称已存在");

            }


            ServiceDetail service;
            if (controllerType.IsSubclassOf(typeof(WebSocketController)))
            {
                service = new ServiceDetail
                {
                    Name = serviceName,
                    Description = description,
                    Type = ServiceType.WebSocket,
                    AllowGatewayProxy = allowGatewayProxy??true,
                };
            }
            else
            {
                service = new ServiceDetail
                {
                    Name = serviceName,
                    Description = description,
                    Type = ServiceType.JmsService,
                    AllowGatewayProxy = allowGatewayProxy??false,
                };
            }

            _services.AddScoped(controllerType);
            _ControllerFactory.RegisterController(controllerType, service);

        }


        /// <summary>
        /// 设置服务可用
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="enable"></param>
        public void SetServiceEnable(string serviceName, bool enable)
        {
            _ControllerFactory.SetControllerEnable(serviceName, enable);
            _GatewayConnector?.OnServiceInfoChanged();
        }


        /// <summary>
        /// 注册定时任务，任务在MicroServiceHost.Run时，按计划执行
        /// </summary>
        /// <typeparam name="T">定时任务的类，必须实现IScheduleTask（注册的类会自动支持依赖注入）</typeparam>
        public void RegisterScheduleTask<T>() where T : IScheduleTask
        {
            var type = typeof(T);
            _services.AddTransient(type);
            _scheduleTaskManager.AddTask(type);
        }

        /// <summary>
        /// 注销定时任务
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void UnRegisterScheduleTask<T>() where T : IScheduleTask
        {
            _scheduleTaskManager.RemoveTask(typeof(T));
        }

        void registerServices()
        {

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _services.AddSingleton<ICpuInfo, CpuInfoForLinux>();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _services.AddSingleton<ICpuInfo, CpuInfoForWin>();
            }
            else
            {
                _services.AddSingleton<ICpuInfo, CpuInfoForUnkown>();
            }
            _services.AddSingleton<IConnectionCounter, ConnectionCounter>();
            _services.AddSingleton<FaildCommitBuilder>();
            _services.AddSingleton<RetryCommitMission>();
            _services.AddSingleton<SSLConfiguration>(new SSLConfiguration());
            _services.AddSingleton<ScheduleTaskManager>(_scheduleTaskManager);
            _services.AddTransient<ScheduleTaskController>();
            _services.AddSingleton<IKeyLocker, KeyLocker>();
            _services.AddSingleton<ICodeBuilder, CodeBuilder>();
            _services.AddSingleton<IGatewayConnector, GatewayConnector>();
            _services.AddSingleton<IRequestReception, RequestReception>();
            _services.AddSingleton<IProcessExitHandler, ProcessExitHandler>();
            _services.AddSingleton<MicroServiceHost>(this);
            _services.AddSingleton<SafeTaskFactory>();

            _services.AddSingleton<ControllerFactory>(_ControllerFactory);

            //注入handler
            var handlerTypes = typeof(RequestReception).Assembly.DefinedTypes.Where(m => m.ImplementedInterfaces.Contains(typeof(IRequestHandler))).Select(m=>ServiceDescriptor.Singleton(typeof(IRequestHandler) , m));
            _services.TryAddEnumerable(handlerTypes);

           
        }

        public MicroServiceHost Build(int port, NetAddress[] gatewayAddresses)
        {
            if (gatewayAddresses == null || gatewayAddresses.Length == 0)
                throw new Exception("Gateway addres is empty");
            AllGatewayAddresses = gatewayAddresses;
            this.ServicePort = port;

            return this;
        }


        /// <summary>
        /// 运行服务
        /// </summary>
        public void Run()
        {
            Run(_services.BuildServiceProvider());
        }

        /// <summary>
        /// 在指定的IServiceProvider环境下运行服务
        /// </summary>
        /// <param name="serviceProvider"></param>
        public void Run(IServiceProvider serviceProvider)
        {
            ThreadPool.GetMaxThreads(out int w, out int c);
            ThreadPool.SetMinThreads(w, c);

            ServiceProvider = serviceProvider;
            if (ServicePort == 0 && ServiceAddress == null)
                return;

            _logger = ServiceProvider.GetService<ILogger<MicroServiceHost>>();
            _GatewayConnector = ServiceProvider.GetService<IGatewayConnector>();

            _RequestReception = ServiceProvider.GetService<IRequestReception>();
            _scheduleTaskManager.StartTasks();

            var sslConfig = ServiceProvider.GetService<SSLConfiguration>();

            if (ServicePort != 0 && !_isWebServer)
            {
                _tcpServer = new JMS.ServerCore.MulitTcpListener(ServicePort);
                _tcpServer.Connected += _tcpServer_Connected;
                _logger?.LogInformation("Service host started , port:{0}", ServicePort);
            }
            _logger?.LogInformation("Gateways:" + AllGatewayAddresses.ToJsonString());

            if (sslConfig != null)
            {
                if (sslConfig.ServerCertificate != null)
                    _logger?.LogInformation("Service host use ssl,certificate hash:{0}", sslConfig.ServerCertificate.GetCertHashString());
            }

            _GatewayConnector.ConnectCompleted += _GatewayConnector_ConnectCompleted;
            _GatewayConnector.ConnectAsync();

            _processExitHandler = ServiceProvider.GetService<IProcessExitHandler>();
            ((IProcessExitListener)_processExitHandler).Listen(this);

            if (_tcpServer != null)
            {
                _tcpServer.OnError += _tcpServer_OnError;
                _tcpServer.Run();
            }
        }

        private void _tcpServer_OnError(object sender, Exception e)
        {
            _logger?.LogError(e, "");
        }

        private void _tcpServer_Connected(object sender, Socket socket)
        {
            if (_processExitHandler.ProcessExited)
            {
                _tcpServer?.Stop();
                _tcpServer = null;
                return;
            }
            Task.Run(() => _RequestReception.Interview(socket));
        }

        private void _GatewayConnector_ConnectCompleted(object sender, Version gatewayVersion)
        {
            _GatewayConnector.ConnectCompleted -= _GatewayConnector_ConnectCompleted;

            //实例化FaildCommitBuilder，并重复提交失败的事务
            if (!_isWebServer)
            {
                ServiceProvider.GetService<RetryCommitMission>().OnGatewayReady();
            }

            if(gatewayVersion > new Version("3.1.0.2"))
            {
                //重启服务器，为防止上次可能崩溃，网关还留有分布式锁，所以这里应该清空一下
                ServiceProvider.GetService<IKeyLocker>().UnLockAllKeys();
            }

            if (ServiceProviderBuilded != null)
            {
                new Thread(() =>
                {
                    try
                    {
                        ServiceProviderBuilded(this, this.ServiceProvider);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, ex.Message);
                    }
                }).Start();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _tcpServer?.Stop();
                _tcpServer = null;
                _GatewayConnector.DisconnectGateway();
            }
        }
    }
}
