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
        static ShareFileClient ShareFileClient;
       static ShareFileClient ShareFileClient2;
        static void Main(string[] args)
        {
            ServiceCollection services = new ServiceCollection();

            var gateways = new NetAddress[] {
               new NetAddress{
                    Address = "localhost",
                    Port = 8912
               }
            };
            var msp = new MicroServiceHost(services);
          

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            var configuration = builder.Build();

            //services.UseJmsTokenAuthentication(AuthorizationContentType.String, "127.0.0.1", 9911,"auth");
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);


            msp.ClientCheckCode = @"
            try
            {
               var number = Convert.ToInt64(arg);
                return number > 0;
            }
            catch 
            {
            }
            return false;

";

            msp.Register<Controller1>("Controller1");
            msp.Register<Controller2>("Service2");
            msp.RegisterScheduleTask<AutoRun1>();
            msp.ServiceProviderBuilded += Msp_ServiceProviderBuilded;
            msp.Build(8913, gateways)
                .Run();
        }

        private static void Msp_ServiceProviderBuilded(object sender, IServiceProvider e)
        {
            ShareFileClient = new ShareFileClient(new NetAddress
            {
                Address = "localhost",
                Port = 8911
            }, e.GetService<ILogger<Program>>() );
            ShareFileClient.MapShareFileToLocal("FllowOrderSystem/textDict.json", "./textDict.json", null);
            ShareFileClient.StartListen();
            try
            {
               // ShareFileClient.GetGatewayShareFile("FllowOrderSystem/textDict.json", "./textDict.json");
            }
            catch (Exception ex)
            {
            }



            ShareFileClient2 = new ShareFileClient(new NetAddress
            {
                Address = "localhost",
                Port = 8911
            }, e.GetService<ILogger<Program>>());
            ShareFileClient2.MapShareFileToLocal("FllowOrderSystem/textDict.json", "./textDict2.json", null);
            ShareFileClient2.StartListen();
            try
            {
                //ShareFileClient2.GetGatewayShareFile("FllowOrderSystem/textDict.json", "./textDict2.json");
            }
            catch (Exception ex)
            {
            }
        }
    }
}
