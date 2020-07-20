using JMS.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace JMS
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                try
                {
                    NetClient client = new NetClient("127.0.0.1", 8911);
                    client.Dispose();
                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }

            var gatewaycert = new System.Security.Cryptography.X509Certificates.X509Certificate2("../../../../pfx/client.pfx", "123456");

            ServiceCollection services = new ServiceCollection();

            var gateways = new NetAddress[] {
               new NetAddress{
                    Address = "localhost",
                    Port = 8911
               }
            };
            var msp = new MicroServiceHost(services);
            if (File.Exists("./appsettings.json") == false)
            {
                //本地没有appsettings.json，先从网关拉一个
                msp.GetGatewayShareFile(gateways[0], "test/appsettings.json", "./appsettings.json", gatewaycert);
            }


            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            var configuration = builder.Build();

           
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);

            msp.MapShareFileToLocal(gateways[0] , "test/appsettings.json", "./appsettings.json",(p,p2)=> {
                Console.WriteLine(p + "回调");
            });
            msp.MapShareFileToLocal(gateways[0], "test/appsettings2.json", "./appsettings2.json");


            msp.Register<Controller1>("Controller1");
            msp.Register<Controller2>("Service2");
            msp.Build(8912, gateways)
                .UseSSL(c =>
                { //配置ssl
                    c.GatewayClientCertificate = gatewaycert;
                    c.ServerCertificate = new X509Certificate2("../../../../pfx/service_server.pfx", "123456");
                })
                .Run();
        }
    }
}
