using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace JMS.Proxy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ThreadPool.GetMinThreads(out int w, out int c);
            if (c < 500)
            {
                ThreadPool.SetMinThreads(500, 500);
            }

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configuration = builder.Build();

            int port;
            if(args.Length > 0)
            {
                port = int.Parse(args[0]);
            }
            else
            {
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

            var proxy = serviceProvider.GetService<Socks5Server>();

            proxy.Run(port);
        }
    }
}
