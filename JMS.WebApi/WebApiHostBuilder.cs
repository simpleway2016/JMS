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
using System.Security.Authentication;

namespace JMS
{
    public class WebApiHostBuilder
    {
        private readonly string[] _args;
        public JmsServiceCollection Services { get; }
        public IConfiguration Configuration { get; private set; }

        CommandArgParser _cmdArg;
        string _appSettingPath;
        WebApiHostBuilder(string[] args)
        {
            _args = args;
            this.Services = new JmsServiceCollection();

            _cmdArg = new CommandArgParser(_args);
            _appSettingPath = _cmdArg.TryGetValue<string>("-s");

            if (_appSettingPath == null)
                _appSettingPath = "appsettings.json";

            var builder = new ConfigurationBuilder();
            if (_appSettingPath == "share")
            {
                _appSettingPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                _appSettingPath = Path.Combine(_appSettingPath, "jms.webapi");
                if (Directory.Exists(_appSettingPath) == false)
                {
                    Directory.CreateDirectory(_appSettingPath);
                }
                _appSettingPath = Path.Combine(_appSettingPath, "appsettings.json");
                if (File.Exists(_appSettingPath) == false)
                {
                    File.Copy("./appsettings.json", _appSettingPath);
                }
            }

            builder.AddJsonFile(_appSettingPath, optional: true, reloadOnChange: true);
            Configuration = builder.Build();
            Services.AddSingleton(Configuration);
        }

        public WebApiHost Build()
        {
            this.Services.AddSingleton(Configuration);

            var port = Configuration.GetValue<int>("Port");

            port = _cmdArg.TryGetValue<int>("-p", port);

            var webApiEnvironment = new DefaultWebApiHostEnvironment(_appSettingPath, port);
            this.Services.AddSingleton<IWebApiHostEnvironment>(webApiEnvironment);


            Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(Configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
          
            Services.AddSingleton<IRequestReception, RequestReception>();
            Services.AddSingleton<HttpRequestHandler>();
            Services.AddSingleton<RequestTimeLimter>();
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
                var sslProtocols = Configuration.GetSection("SSL:SslProtocols").Get<SslProtocols?>();
                if(sslProtocols != null)
                {
                    webApiEnvironment.SslProtocol = sslProtocols.Value;
                }
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
