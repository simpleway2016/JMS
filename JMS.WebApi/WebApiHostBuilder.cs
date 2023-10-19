using JMS.Applications.CommandHandles;
using JMS.Applications.HttpMiddlewares;
using JMS.Applications;
using JMS.Common;
using JMS.ServerCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMS.ServerCore.Http;
using Microsoft.Extensions.Logging;

namespace JMS
{
    public class WebApiHostBuilder
    {
        private readonly string[] _args;
        public JmsServiceCollection Services { get; }
        public IConfiguration Configuration { get; private set; }
        WebApiHostBuilder(string[] args)
        {
            _args = args;
            this.Services = new JmsServiceCollection();
        }

        public WebApiHost Build()
        {

            CommandArgParser cmdArg = new CommandArgParser(_args);
            var appSettingPath = cmdArg.TryGetValue<string>("-s");

            if (appSettingPath == null)
                appSettingPath = "appsettings.json";

            var builder = new ConfigurationBuilder();
            if (appSettingPath == "share")
            {
                appSettingPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                appSettingPath = Path.Combine(appSettingPath, "jms.webapi");
                if (Directory.Exists(appSettingPath) == false)
                {
                    Directory.CreateDirectory(appSettingPath);
                }
                appSettingPath = Path.Combine(appSettingPath, "appsettings.json");
                if (File.Exists(appSettingPath) == false)
                {
                    File.Copy("./appsettings.json", appSettingPath);
                }
            }

            builder.AddJsonFile(appSettingPath, optional: true, reloadOnChange: true);
            Configuration = builder.Build();

            this.Services.AddSingleton(Configuration);

            var port = Configuration.GetValue<int>("Port");

            port = cmdArg.TryGetValue<int>("-p", port);

            var webApiEnvironment = new DefaultWebApiHostEnvironment(appSettingPath, port);
            this.Services.AddSingleton<IWebApiHostEnvironment>(webApiEnvironment);


            Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(Configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            Services.AddSingleton<IConfiguration>(Configuration);
            Services.AddSingleton<IRequestReception, RequestReception>();
            Services.AddSingleton<HttpRequestHandler>();
            Services.AddSingleton<WebApiHost>();
            Services.UseHttp()
                .AddHttpMiddleware<WebSocketMiddleware>()
                .AddHttpMiddleware<JmsDocMiddleware>()
                .AddHttpMiddleware<ProxyMiddleware>();

            var serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetService<IHttpMiddlewareManager>().PrepareMiddlewares(serviceProvider);

            webApiEnvironment.GatewayAddresses = Configuration.GetSection("Gateways").Get<NetAddress[]>();
            var server = serviceProvider.GetService<WebApiHost>();

            //SSL
            var certPath = Configuration.GetValue<string>("SSL:Cert");
            if (!string.IsNullOrEmpty(certPath))
            {
                webApiEnvironment.ServerCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, Configuration.GetValue<string>("SSL:Password"));
                webApiEnvironment.AcceptCertHash = Configuration.GetSection("SSL:AcceptCertHash").Get<string[]>();
            }

            server.ServiceProvider = serviceProvider;

            return server;
        }

        public static WebApiHostBuilder Create(string[] args)
        {
            return new WebApiHostBuilder(args);
        }
    }
}
