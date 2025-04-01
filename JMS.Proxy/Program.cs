﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.Proxy
{
    public class Program
    {
        static Socks5Server _socks5Server;
        public static async Task Main(string[] args)
        {
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 10, Environment.ProcessorCount * 10);

            var builder = new ConfigurationBuilder();
            string appsettingFileName = "appsettings.json";

            IConfiguration configuration;
            int port;
            if (args.Length > 0)
            {
                port = int.Parse(args[0]);
                if (args.Length > 1)
                {
                    appsettingFileName = args[1];
                }

                builder.AddJsonFile(appsettingFileName, optional: true, reloadOnChange: true);
                configuration = builder.Build();
            }
            else
            {
                builder.AddJsonFile(appsettingFileName, optional: true, reloadOnChange: true);
                configuration = builder.Build();

                port = configuration.GetValue<int>("Port");
            }


            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);

            services.AddSingleton<Socks5Server>();
            services.AddSingleton<RequestHandler>();
            var serviceProvider = services.BuildServiceProvider();

            _socks5Server = serviceProvider.GetService<Socks5Server>();
            ConfigurationChangeCallback(configuration);
            await _socks5Server.RunAsync(port);
        }

        static IDisposable CallbackRegistration;
        static void ConfigurationChangeCallback(object p)
        {
            CallbackRegistration?.Dispose();
            CallbackRegistration = null;

            IConfiguration configuration = (IConfiguration)p;
            Task.Run(() => {
                Thread.Sleep(1000);//延迟注册，否则可能每次都回调两次
                CallbackRegistration = configuration.GetReloadToken().RegisterChangeCallback(ConfigurationChangeCallback, configuration);
            });
            _socks5Server.WhiteList = configuration.GetSection("WhiteList").Get<NetAddress[]>();
        }
    }
}
