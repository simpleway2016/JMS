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
using static Org.BouncyCastle.Math.EC.ECCurve;
using JMS.WebApi;

namespace JMS
{
    public class WebApiHostBuilder
    {
        private readonly string[] _args;
        public JmsServiceCollection Services { get; }
        IConfiguration _configuration;
        CommandArgParser _cmdArg;
        string _appSettingPath;

        public ConfigurationValue<WebApiConfig> Config;

        WebApiHostBuilder(string[] args)
        {
            _args = args;
            this.Services = new JmsServiceCollection();

            _cmdArg = new CommandArgParser(_args);
            _cmdArg.TryGetValue("-s",out _appSettingPath);

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
            _configuration = builder.Build();
            
        }

        public WebApiHost Build()
        {
            Config = _configuration.GetNewest<WebApiConfig>();
            var port = Config.Current.Port;

            var port = Configuration.GetValue<int>("Port");
            if (_cmdArg.TryGetValue("-p", out string strPort))
            {
                int.TryParse(strPort, out port);
            }

            var webApiEnvironment = new DefaultWebApiHostEnvironment(_appSettingPath, port , Config);
            this.Services.AddSingleton<IWebApiHostEnvironment>(webApiEnvironment);


            Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(_configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });

            Services.AddSingleton(_configuration);
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

            var server = serviceProvider.GetService<WebApiHost>();

            server.ServiceProvider = serviceProvider;

            return server;
        }

        public static WebApiHostBuilder Create(string[] args)
        {
            return new WebApiHostBuilder(args);
        }
    }
}
