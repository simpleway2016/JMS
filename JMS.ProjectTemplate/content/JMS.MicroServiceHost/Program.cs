using JMS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace JMS.MicroServiceHost
{
    public class Program
    {
        static void Main(string[] args)
        {
            Run();
        }

        public static void Run(string appsettingPath = "appsettings.json", Action<ServiceCollection> initServiceAction = null)
        {

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile(appsettingPath, optional: true, reloadOnChange: true);
            Global.Configuration = builder.Build();

            var port = Global.Configuration.GetValue<int>("Port"); //提供微服务的端口
            var serviceAddress = Global.Configuration.GetValue<string>("ServiceAddress");

            //网关地址
            Global.GatewayAddresses = Global.Configuration.GetSection("Gateways").Get<NetAddress[]>();

            ServiceCollection services = InitServices();

            var msp = new MicroServiceHost(services);
            if (string.IsNullOrEmpty(serviceAddress) == false)
            {
                //自定义微服务地址
                msp.ServiceAddress = new NetAddress(serviceAddress, port);
            }
            msp.Register<Controllers.DemoController>("DemoService", "测试服务", false);
            msp.ServiceProviderBuilded += Msp_ServiceProviderBuilded;

            initServiceAction?.Invoke(services);

            msp.Build(port, Global.GatewayAddresses, true)
                .Run();
        }

        static ServiceCollection InitServices()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(Global.Configuration);
            services.AddScoped<SystemDBContext>();

            //使用Serilog处理日志
            Global.InitSerilogLog(services);

            return services;
        }

        public static IServiceProvider GetServiceProvider()
        {
            while (Global.ServiceProvider == null)
                System.Threading.Thread.Sleep(100);
            return Global.ServiceProvider;
        }

        private static void Msp_ServiceProviderBuilded(object sender, IServiceProvider e)
        {
            Global.ServiceProvider = e;
            var logger = Global.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("启动完毕");
        }
    }
}
