using JMS;
using JMS.ServiceProvider.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Way.Lib;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class Extens
    {
        static MicroServiceHost Host;
        static NetAddress[] Gateways;
        static IConnectionCounter ConnectionCounter;
        /// <summary>
        /// 把web server注册为JMS微服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="webServerUrl">web服务器的根访问路径，如 http://192.168.2.128:8080</param>
        /// <param name="serviceName">服务名称</param>
        /// <param name="gateways">网关地址</param>
        /// <param name="sslConfig">配置ssl证书</param>
        /// <returns></returns>
        public static IServiceCollection RegisterJmsService(this IServiceCollection services,string webServerUrl , string serviceName, NetAddress[] gateways,Action< SSLConfiguration> sslConfig = null)
        {
            services.AddScoped<ApiTransactionDelegate>();
            services.AddSingleton<ApiRetryCommitMission>();
            services.AddSingleton<ApiFaildCommitBuilder>();
            services.AddSingleton<ControllerFactory>();

            Gateways = gateways;
            MicroServiceHost host = new MicroServiceHost(services);
            host.RegisterWebServer(webServerUrl, serviceName);
            host.UseSSL(sslConfig);
            Host = host;
                return services;
        }

        /// <summary>
        /// 启动JMS微服务
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseJmsService(this IApplicationBuilder app)
        {
            if(Host == null)
            {
                throw new Exception("请先调用services.RegisterJmsService() 注册服务");
            }


            Host.Build(0, Gateways).Run();
            app.Use(async (context, next) => {
                if(ConnectionCounter == null)
                {
                    ConnectionCounter = app.ApplicationServices.GetService<IConnectionCounter>();
                }

                ConnectionCounter.OnConnect();
                try
                {
                    if (HttpHandler.Handle(app, context) == false)
                    {
                        await next();
                    }
                }
                catch (Exception)
                {

                    throw;
                }
                finally
                {
                    ConnectionCounter.OnDisconnect();
                }
            });

            

            return app;
        }

    }
}
