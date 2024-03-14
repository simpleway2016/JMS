using JMS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;

namespace UserInfoServiceHost
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
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
            msp.Register<Controllers.UserInfoController>("UserInfoService", "用户服务", true);
            msp.ServiceProviderBuilded += Msp_ServiceProviderBuilded;
            msp.Build(port, Global.GatewayAddresses)
                .Run();
        }

        public static ServiceCollection InitServices()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(Global.Configuration);
            services.AddScoped<SystemDBContext>();

            //初始化数据库
            initDatabase();

            //使用Serilog处理日志
            Global.InitSerilogLog();
            services.AddLogging(builder =>
            {
                builder.AddSerilog();
            });

            return services;
        }

        static void initDatabase()
        {
            using var db = new SystemDBContext();
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS UserInfo (  
    id INTEGER PRIMARY KEY AUTOINCREMENT,  
    username VARCHAR(50) NOT NULL,  
    password VARCHAR(50)  
);
";
            cmd.ExecuteNonQuery();
        }

        private static void Msp_ServiceProviderBuilded(object sender, IServiceProvider e)
        {
            Global.ServiceProvider = e;

        }
    }
}
