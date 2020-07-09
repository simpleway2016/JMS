using JMS.Common.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace JMS
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.Sleep(1000);

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            var configuration = builder.Build();

            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);

            var msp = new MicroServiceHost(services);
            msp.Register<Controller1>("Controller1");
            msp.Register<Controller2>("Service2");

            var gateways = new NetAddress[] {
               new NetAddress{
                    Address = "localhost",
                    Port = 8911
               }
            };

            msp.Build(8912, gateways)
                .UseTransactionRecorder(o=> {
                    o.TransactionLogFolder = "./tranlogs";
                })
                .UseSSL(c=> {
                    c.GatewayClientCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2("d:/test.pfx", "123456");
                    //c.ServerCertificate = c.GatewayClientCertificate;
                })
                .Run();
        }
    }
}
