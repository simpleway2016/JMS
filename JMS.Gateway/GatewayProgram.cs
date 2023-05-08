using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using JMS.Domains;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Linq;
using JMS.Common;
using System.Diagnostics;
using JMS.Applications;
using JMS.Infrastructures;
using System.Threading;
using System.IO;
using JMS.ServerCore;
using JMS.ServerCore.Http.Middlewares;
using JMS.ServerCore.Http;
using JMS.Applications.HttpMiddlewares;

namespace JMS
{
    public class GatewayProgram
    {
        internal static string AppSettingPath;
        static void Main(string[] args)
        {
            if(args.Length > 1&& args[0].EndsWith(".pfx") )
            {
                var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(args[0], args[1]);
                Console.WriteLine(cert.GetCertHashString());
                return;
            }
            //固定当前工作目录
            System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ThreadPool.GetMaxThreads(out int w, out int c);
            ThreadPool.SetMinThreads(w, c);

            CommandArgParser cmdArg = new CommandArgParser(args);
            AppSettingPath = cmdArg.TryGetValue<string>("-s");

            if (AppSettingPath == null)
                AppSettingPath = "appsettings.json";

            var builder = new ConfigurationBuilder();
            if (AppSettingPath == "share")
            {
                AppSettingPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                AppSettingPath = Path.Combine(AppSettingPath, "jms.gateway");
                if (Directory.Exists(AppSettingPath) == false)
                {
                    Directory.CreateDirectory(AppSettingPath);
                }
                AppSettingPath = Path.Combine(AppSettingPath, "appsettings.json");
                if (File.Exists(AppSettingPath) == false)
                {
                    File.Copy("./appsettings.json", AppSettingPath);
                }
            }

            builder.AddJsonFile(AppSettingPath, optional: true, reloadOnChange: true);
            var configuration = builder.Build();

            var port = configuration.GetValue<int>("Port");
            
            port = cmdArg.TryGetValue<int>("-p", port);

            Run(configuration,port,out Gateway gatewayInstance);
        }

        public static void Run(IConfiguration configuration,int port,out Gateway gatewayInstance)
        {
           
            var sharefolder = configuration.GetValue<string>("ShareFolder");
            if (!System.IO.Directory.Exists(sharefolder))
            {
                System.IO.Directory.CreateDirectory(sharefolder);
            }

            var datafolder = configuration.GetValue<string>("DataFolder");
            if (!System.IO.Directory.Exists(datafolder))
            {
                System.IO.Directory.CreateDirectory(datafolder);
            }

            JmsServiceCollection services = new JmsServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<ClusterGatewayConnector>();
            services.AddSingleton<TransactionStatusManager>();
            services.AddSingleton<IRequestReception, RequestReception>();
            services.AddSingleton<IRegisterServiceManager, RegisterServiceManager>();
            services.AddSingleton<ICommandHandlerRoute, CommandHandlerRoute>();
            services.AddSingleton<IRemoteClientManager, DefaultRemoteClientManager>();
            services.AddSingleton<Gateway>();
            services.AddSingleton<LockKeyManager>();
            services.AddTransient<IMicroServiceReception, MicroServiceReception>();
            services.AddSingleton<FileChangeWatcher>();
            services.AddTransient<ListenFileChangeReception>();
            services.AddSingleton<ClientCheckFactory>();
            services.AddSingleton<ErrorUserMarker>();

            services.UseHttp()
                .AddHttpMiddleware<WebSocketMiddleware>()
                .AddHttpMiddleware<FunctionRequestMiddleware>()
                .AddHttpMiddleware<JmsDocMiddleware>()
                .AddHttpMiddleware<ProxyMiddleware>();

            var assembly = Assembly.Load(configuration.GetValue<string>("ServiceProviderAllocator:Assembly"));
            var serviceProviderAllocatorType = assembly.GetType(configuration.GetValue<string>("ServiceProviderAllocator:FullName"));

            services.AddSingleton(typeof(IServiceProviderAllocator), serviceProviderAllocatorType);
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetService<LockKeyManager>();
            serviceProvider.GetService<FileChangeWatcher>();
            serviceProvider.GetService<TransactionStatusManager>();
            serviceProvider.GetService<ICommandHandlerRoute>().Init();

            serviceProvider.GetService<IHttpMiddlewareManager>().PrepareMiddlewares(serviceProvider);

            var gateway = serviceProvider.GetService<Gateway>();

            //SSL
            var certPath = configuration.GetValue<string>("SSL:Cert");
            if (!string.IsNullOrEmpty(certPath))
            {
                gateway.ServerCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, configuration.GetValue<string>("SSL:Password"));
                gateway.AcceptCertHash = configuration.GetSection("SSL:AcceptCertHash").Get<string[]>();
            }

            gateway.ServiceProvider = serviceProvider;
            gatewayInstance = gateway;
            gateway.Run(port);
        }

    }
}
