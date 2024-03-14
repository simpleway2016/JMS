﻿using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Linq;
using JMS.Common;
using System.Diagnostics;
using JMS.Applications;
using System.Threading;
using System.IO;
using JMS.Applications.CommandHandles;
using JMS.HttpProxy;

namespace JMS
{
    public class HttpProxyProgram
    {
        internal static string AppSettingPath;
        internal static IConfiguration Configuration;
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
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 10, Environment.ProcessorCount * 10);

            CommandArgParser cmdArg = new CommandArgParser(args);
            AppSettingPath = cmdArg.TryGetValue<string>("-s");

            if (AppSettingPath == null)
                AppSettingPath = "appsettings.json";

            var builder = new ConfigurationBuilder();
            if (AppSettingPath == "share")
            {
                AppSettingPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                AppSettingPath = Path.Combine(AppSettingPath, "jms.httpproxy");
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
            Configuration = builder.Build();

            var port = Configuration.GetValue<int>("Port");
            
            port = cmdArg.TryGetValue<int>("-p", port);

            Run(Configuration,port,out HttpProxyServer webapiInstance);
        }

        public static void Run(IConfiguration configuration,int port,out HttpProxyServer webapiInstance)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IRequestReception, RequestReception>();
            services.AddSingleton<HttpRequestHandler>();
            services.AddSingleton<HttpProxyServer>();
            services.AddSingleton<RequestTimeLimter>();
          
            var serviceProvider = services.BuildServiceProvider();

            var server = serviceProvider.GetService<HttpProxyServer>();

            //SSL
            var certPath = configuration.GetValue<string>("SSL:Cert");
            if (!string.IsNullOrEmpty(certPath))
            {
                server.ServerCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, configuration.GetValue<string>("SSL:Password"));
            }

            server.ServiceProvider = serviceProvider;
            webapiInstance = server;
            server.Run(port);
        }

    }
}
