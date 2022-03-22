using JMS.GenerateCode;
using JMS.Impls;
using JMS.Interfaces;
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
using JMS.Interfaces.Hardware;
using JMS.Impls.Haredware;
using System.Security.Cryptography.X509Certificates;
using JMS.RetryCommit;

namespace JMS
{
    public class MicroServiceHost
    {
        public string Id { get; private set; }
        ILogger<MicroServiceHost> _logger;
        IGatewayConnector _GatewayConnector;
        internal IGatewayConnector GatewayConnector => _GatewayConnector;
        public NetAddress MasterGatewayAddress { internal set; get; }
        public NetAddress[] AllGatewayAddresses { get; private set; }

        internal Dictionary<string, ControllerTypeInfo> ServiceNames = new Dictionary<string, ControllerTypeInfo>();
        internal int ServicePort;

        /// <summary>
        /// 当前处理中的请求数
        /// </summary>
        internal int ClientConnected;
        public IServiceProvider ServiceProvider { private set; get; }
        /// <summary>
        /// 设置微服务的地址，如果为null，网关会使用微服务的外网ip作为服务地址
        /// </summary>
        public NetAddress ServiceAddress { get; set; }

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
                    _GatewayConnector?.OnServiceNameListChanged();
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

        private string _ClientCheckCode;
        /// <summary>
        /// 自定义客户端检验代码
        /// </summary>
        public string ClientCheckCode
        {
            get => _ClientCheckCode;
            set
            {
                if (_ClientCheckCode != value)
                {
                    _ClientCheckCode = value;
                    _GatewayConnector?.OnServiceNameListChanged();
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
                if(_RetryCommitPath != value)
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
        public void Register<T>(string serviceName) where T : MicroServiceControllerBase
        {
            this.Register(typeof(T), serviceName);
        }

        bool _isWebServer;
        /// <summary>
        /// 把当前程序注册为一个web服务器，并且Run时不再启动指定的网络端口
        /// </summary>
        /// <param name="webServerUrl">web服务器的根访问路径，如 http://192.168.2.128:8080 </param>
        /// <param name="serverName">服务名称，默认为WebServer</param>
        public void RegisterWebServer(string webServerUrl,string serverName = "WebServer")
        {
            _isWebServer = true;
            this.ServiceAddress = new NetAddress(webServerUrl, 0);
             ServiceNames[serverName] = new ControllerTypeInfo()
            {
                Enable = true
            };
        }

        /// <summary>
        /// 向网关注册服务
        /// </summary>
        /// <param name="contollerType">Controller类型</param>
        /// <param name="serviceName">服务名称</param>
        public void Register(Type contollerType, string serviceName)
        {
            _services.AddTransient(contollerType);
            ServiceNames[serviceName] = new ControllerTypeInfo()
            {
                Type = contollerType,
                Enable = true,
                NeedAuthorize = contollerType.GetCustomAttribute<AuthorizeAttribute>() != null,
                Methods = contollerType.GetTypeInfo().DeclaredMethods.Where(m =>
                    m.IsStatic == false &&
                    m.IsPublic &&
                    m.DeclaringType != typeof(MicroServiceControllerBase)
                    && m.DeclaringType != typeof(object)).OrderBy(m=>m.Name).Select(m=>new TypeMethodInfo { 
                        Method = m,
                        NeedAuthorize = m.GetCustomAttribute<AuthorizeAttribute>() != null
                    }).ToArray()
            };
            
        }

        /// <summary>
        /// 设置服务可用
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="enable"></param>
        public void SetServiceEnable(string serviceName, bool enable)
        {
            ServiceNames[serviceName].Enable = enable;
            _GatewayConnector?.OnServiceNameListChanged();
        }


        /// <summary>
        /// 注册定时任务，任务在MicroServiceHost.Run时，按计划执行
        /// </summary>
        /// <typeparam name="T">定时任务的类，必须实现IScheduleTask（注册的类会自动支持依赖注入）</typeparam>
        public void RegisterScheduleTask<T>() where T: IScheduleTask
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
           
            if(RuntimeInformation.IsOSPlatform( OSPlatform.Linux ))
            {
                _services.AddSingleton<ICpuInfo,CpuInfoForLinux>();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _services.AddSingleton<ICpuInfo, CpuInfoForWin>();
            }
            else
            {
                _services.AddSingleton<ICpuInfo, CpuInfoForUnkown>();
            }
            _services.AddSingleton<FaildCommitBuilder>();
            _services.AddSingleton<RetryCommitMission>();
            _services.AddSingleton<SSLConfiguration>(new SSLConfiguration());
            _services.AddSingleton<ScheduleTaskManager>(_scheduleTaskManager);
            _services.AddTransient<ScheduleTaskController>();
            _services.AddSingleton<IKeyLocker, KeyLocker>();
            _services.AddSingleton<ICodeBuilder, CodeBuilder>();
            _services.AddSingleton<IGatewayConnector, GatewayConnector>();
            _services.AddSingleton<IRequestReception, RequestReception>();
            _services.AddSingleton<InvokeRequestHandler>();
            _services.AddSingleton<GenerateInvokeCodeRequestHandler>();
            _services.AddSingleton<CommitRequestHandler>();
            _services.AddSingleton<GetAllLockedKeysHandler>();
            _services.AddSingleton<UnLockedKeyAnywayHandler>();
            _services.AddSingleton<RollbackRequestHandler>();
            _services.AddSingleton<IProcessExitHandler,ProcessExitHandler>();
            _services.AddSingleton<MicroServiceHost>(this);
            _services.AddSingleton<TransactionDelegateCenter>();
            _services.AddSingleton<SafeTaskFactory>();
        }

        public MicroServiceHost Build(int port,NetAddress[] gatewayAddresses)
        {
            if (gatewayAddresses == null || gatewayAddresses.Length == 0)
                throw new Exception("Gateway addres is empty");
            AllGatewayAddresses = gatewayAddresses;
            this.ServicePort = port;
            return this;
        }


        public void Run()
        {
            ServiceProvider = _services.BuildServiceProvider();

            _logger = ServiceProvider.GetService<ILogger<MicroServiceHost>>();
            _GatewayConnector = ServiceProvider.GetService<IGatewayConnector>();

            _RequestReception = ServiceProvider.GetService<IRequestReception>();
            _scheduleTaskManager.StartTasks();

            var sslConfig = ServiceProvider.GetService<SSLConfiguration>();

            TcpListener listener = new TcpListener(ServicePort);
            listener.Start();
            _logger?.LogInformation("Service host started , port:{0}",ServicePort);
            _logger?.LogInformation("Gateways:" + AllGatewayAddresses.ToJsonString());

            if (sslConfig != null)
            {
                if(sslConfig.GatewayClientCertificate != null)
                    _logger?.LogInformation("Gateway client use ssl,certificate hash:{0}", sslConfig.GatewayClientCertificate.GetCertHashString());

                if (sslConfig.ServerCertificate != null)
                    _logger?.LogInformation("Service host use ssl,certificate hash:{0}", sslConfig.ServerCertificate.GetCertHashString());
            }

            _GatewayConnector.OnConnectCompleted = () => {
                _GatewayConnector.OnConnectCompleted = null;

                //实例化FaildCommitBuilder，并重复提交失败的事务
                ServiceProvider.GetService<RetryCommitMission>().OnGatewayConnected();

                if (ServiceProviderBuilded != null)
                {
                    Task.Run(() => {
                        try
                        {
                            ServiceProviderBuilded(this, this.ServiceProvider);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, ex.Message);
                        }
                    });
                }
            };
            _GatewayConnector.ConnectAsync();

            if (_isWebServer)
                return;

            using (var processExitHandler = (IProcessExitListener)ServiceProvider.GetService<IProcessExitHandler>())
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
