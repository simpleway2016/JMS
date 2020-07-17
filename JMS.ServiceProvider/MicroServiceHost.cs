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
using JMS.Common.Dtos;
using System.Runtime.InteropServices;
using JMS.Interfaces.Hardware;
using JMS.Impls.Haredware;
using JMS.MapShareFiles;
using System.Security.Cryptography.X509Certificates;

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
        internal ServiceCollection _services;
        IRequestReception _RequestReception;
        ScheduleTaskManager _scheduleTaskManager;
        MapFileManager _mapFileManager;
        bool _registerMyServicesed = false;
        public MicroServiceHost(ServiceCollection services)
        {
            this.Id = Guid.NewGuid().ToString("N");
            _services = services;
            _scheduleTaskManager = new ScheduleTaskManager(this);
            _mapFileManager = new MapFileManager(this);
        }

        internal void DisconnectGateway()
        {
            _GatewayConnector.DisconnectGateway();
        }

        /// <summary>
        /// 映射网关上的共享文件到本地
        /// </summary>
        /// <param name="gatewayAddress">包含共享文件的网关地址</param>
        /// <param name="shareFilePath">共享文件路径</param>
        /// <param name="localFilePath">映射本地的路径</param>
        /// <param name="callback">文件写入本地后，回调委托</param>
        public void MapShareFileToLocal( NetAddress gatewayAddress, string shareFilePath , string localFilePath,Action<string,string> callback = null)
        {
            _mapFileManager.MapShareFileToLocal(gatewayAddress, shareFilePath, localFilePath, callback);
        }
        /// <summary>
        /// 获取网关共享文件，并保存到本地
        /// </summary>
        /// <param name="gatewayAddress">包含共享文件的网关地址</param>
        /// <param name="filepath">共享文件路径</param>
        /// <param name="localFilePath">保存到本地的路径</param>
        /// <param name="gatewayClientCert">网关客户端证书</param>
        public void GetGatewayShareFile(NetAddress gatewayAddress, string filepath, string localFilePath, X509Certificate2 gatewayClientCert = null)
        {
            _mapFileManager.GetGatewayShareFile(gatewayAddress, filepath, localFilePath, gatewayClientCert);
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

        /// <summary>
        /// 向网关注册服务
        /// </summary>
        /// <param name="contollerType">Controller类型</param>
        /// <param name="serviceName">服务名称</param>
        public void Register(Type contollerType, string serviceName)
        {
            registerServices();

            _services.AddTransient(contollerType);
            ServiceNames[serviceName] = new ControllerTypeInfo()
            {
                Type = contollerType,
                Methods = contollerType.GetTypeInfo().DeclaredMethods.Where(m =>
                    m.IsStatic == false &&
                    m.IsPublic &&
                    m.DeclaringType != typeof(MicroServiceControllerBase)
                    && m.DeclaringType != typeof(object)).ToArray()
            };
        }

        /// <summary>
        /// 注册定时任务
        /// </summary>
        /// <param name="task"></param>
        public void RegisterScheduleTask(IScheduleTask task)
        {
            _scheduleTaskManager.AddTask(task);
        }

        /// <summary>
        /// 注销定时任务
        /// </summary>
        /// <param name="task"></param>
        public void UnRegisterScheduleTask(IScheduleTask task)
        {
            _scheduleTaskManager.RemoveTask(task);
        }

        void registerServices()
        {
            if (_registerMyServicesed)
                return;
            _registerMyServicesed = true;

           
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
            _services.AddSingleton<SSLConfiguration>(new SSLConfiguration());
            _services.AddSingleton<MapFileManager>(_mapFileManager);
            _services.AddSingleton<ScheduleTaskManager>(_scheduleTaskManager);
            _services.AddTransient<ScheduleTaskController>();
            _services.AddSingleton<IKeyLocker, KeyLocker>();
            _services.AddSingleton<ICodeBuilder, CodeBuilder>();
            _services.AddSingleton<IGatewayConnector, GatewayConnector>();
            _services.AddSingleton<IRequestReception, RequestReception>();
            _services.AddSingleton<InvokeRequestHandler>();
            _services.AddSingleton<GenerateInvokeCodeRequestHandler>();
            _services.AddSingleton<CommitRequestHandler>();
            _services.AddSingleton<RollbackRequestHandler>();
            _services.AddSingleton<ProcessExitHandler>();
            _services.AddSingleton<MicroServiceHost>(this);
            _services.AddSingleton<TransactionDelegateCenter>();
        }

        public MicroServiceHost Build(int port,NetAddress[] gatewayAddresses)
        {
            AllGatewayAddresses = gatewayAddresses;
            this.ServicePort = port;
            return this;
        }

        public void Run()
        {
            ServiceProvider = _services.BuildServiceProvider();

            _logger = ServiceProvider.GetService<ILogger<MicroServiceHost>>();
            _GatewayConnector = ServiceProvider.GetService<IGatewayConnector>();

            _GatewayConnector.ConnectAsync();
            
            _RequestReception = ServiceProvider.GetService<IRequestReception>();
            _scheduleTaskManager.StartTasks();

            _mapFileManager.Start();

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
