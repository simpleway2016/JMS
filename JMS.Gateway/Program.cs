using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using JMS.Interfaces;
using JMS.Impls;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Linq;
using JMS.Common;
using System.Diagnostics;
using Natasha.CSharp;

namespace JMS
{
    class Program
    {
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

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configuration = builder.Build();

            var port = configuration.GetValue<int>("Port");
            CommandArgParser cmdArg = new CommandArgParser(args);
            port = cmdArg.TryGetValue<int>("port", port);

            var datafolder = configuration.GetValue<string>("DataFolder");
            if (!System.IO.Directory.Exists(datafolder))
            {
                System.IO.Directory.CreateDirectory(datafolder);
            }
            datafolder = cmdArg.TryGetValue<string>("DataFolder", datafolder);

            var sharefolder = configuration.GetValue<string>("ShareFolder");
            if (!System.IO.Directory.Exists(sharefolder))
            {
                System.IO.Directory.CreateDirectory(sharefolder);
            }

            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<GatewayRefereeClient>();
            services.AddSingleton<IRequestReception,RequestReception>();
            services.AddSingleton<IRegisterServiceManager, RegisterServiceManager>();
            services.AddSingleton<ICommandHandlerManager, CommandHandlerManager>();
            services.AddSingleton<Gateway>();
            services.AddSingleton<LockKeyManager>();
            services.AddTransient<IMicroServiceReception,MicroServiceReception>();
            services.AddSingleton<TransactionIdBuilder>();
            services.AddSingleton<FileChangeWatcher>();
            services.AddTransient<ListenFileChangeReception>();

            var assembly = Assembly.Load(configuration.GetValue<string>("ServiceProviderAllocator:Assembly"));
            var serviceProviderAllocatorType = assembly.GetType(configuration.GetValue<string>("ServiceProviderAllocator:FullName"));

            services.AddSingleton(typeof(IServiceProviderAllocator), serviceProviderAllocatorType);
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetService<LockKeyManager>();
            serviceProvider.GetService<FileChangeWatcher>();

            //启动GatewayRefereeClient，申请成为主网关
            serviceProvider.GetService<GatewayRefereeClient>();

            var gateway = serviceProvider.GetService<Gateway>();

            //SSL
            var certPath = configuration.GetValue<string>("SSL:Cert");
            if(!string.IsNullOrEmpty(certPath))
            {
                gateway.ServerCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, configuration.GetValue<string>("SSL:Password"));
                gateway.AcceptCertHash = configuration.GetSection("SSL:AcceptCertHash").Get<string[]>();
            }

            gateway.ServiceProvider = serviceProvider;
            gateway.Run(port);
        }

    }
}
