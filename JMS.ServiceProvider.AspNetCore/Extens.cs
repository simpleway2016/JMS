using JMS;
using JMS.ServiceProvider.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Linq;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class Extens
    {
        static ConcurrentDictionary<IServiceCollection, MicroServiceHost> Hosts = new ConcurrentDictionary<IServiceCollection, MicroServiceHost>();
        static NetAddress[] Gateways;
        static IConnectionCounter ConnectionCounter;
        /// <summary>
        /// 把web server注册为JMS微服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="webServerUrl">web服务器的根访问路径，如 http://192.168.2.128:8080</param>
        /// <param name="serviceName">服务名称</param>
        /// <param name="gateways">网关地址</param>
        /// <param name="allowGatewayProxy">允许通过网关反向代理访问此服务</param>
        /// <param name="configOption">配置更多可选项</param>
        /// <param name="sslConfig">配置ssl证书</param>
        /// <returns></returns>
        public static IServiceCollection RegisterJmsService(this IServiceCollection services, string webServerUrl, string serviceName, NetAddress[] gateways,bool allowGatewayProxy = false, Action<IMicroServiceOption> configOption = null, Action<SSLConfiguration> sslConfig = null)
        {
           return RegisterJmsService(services,webServerUrl,serviceName,null,gateways ,allowGatewayProxy, configOption,sslConfig);
        }

        /// <summary>
        /// 把web server注册为JMS微服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="webServerUrl">web服务器的根访问路径，如 http://192.168.2.128:8080</param>
        /// <param name="serviceName">服务名称</param>
        /// <param name="description">服务描述</param>
        /// <param name="gateways">网关地址</param>
        /// <param name="allowGatewayProxy">允许通过网关反向代理访问此服务</param>
        /// <param name="configOption">配置更多可选项</param>
        /// <param name="sslConfig">配置ssl证书</param>
        /// <returns></returns>
        public static IServiceCollection RegisterJmsService(this IServiceCollection services, string webServerUrl, string serviceName,string description, NetAddress[] gateways, bool allowGatewayProxy = false, Action<IMicroServiceOption> configOption = null, Action<SSLConfiguration> sslConfig = null)
        {
            try
            {
                var uri = new Uri(webServerUrl);
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"{webServerUrl}不是有效的url地址");
            }
            MicroServiceHost host = null;
            if (Hosts.ContainsKey(services) == false)
            {
                services.AddScoped<ApiTransactionDelegate>();
                services.AddSingleton<ApiRetryCommitMission>();
                services.AddSingleton<ApiFaildCommitBuilder>();
                services.AddSingleton<ControllerFactory>();
                services.AddScoped<IScopedKeyLocker, DefaultAspNetScopedKeyLocker>();

                 Gateways = gateways;
                host = new MicroServiceHost(services);
                services.AddSingleton<MicroServiceHost>(host);
               
                Hosts[services] = host;
            }
            else
            {
                host = Hosts[services];
            }

            if(sslConfig != null)
            {
                host.UseSSL(sslConfig);                
            }
            if (configOption != null)
            {
                configOption(host);
            }
            host.RegisterWebServer(webServerUrl, serviceName,description , allowGatewayProxy);
            return services;
        }

        /// <summary>
        /// 启动JMS微服务
        /// </summary>
        /// <param name="app"></param>
        /// <param name="onRegister">当微服务注册成功后的回调事件</param>
        /// <returns></returns>
        public static IApplicationBuilder UseJmsService(this IApplicationBuilder app,Action onRegister = null)
        {
           
            var host = app.ApplicationServices.GetService<MicroServiceHost>();
            if (host == null)
            {
                throw new Exception("请先调用services.RegisterJmsService() 注册服务");
            }


            host.ServiceProviderBuilded += (s, e) => {
                var retryEngine = app.ApplicationServices.GetService<ApiRetryCommitMission>();
                retryEngine.OnGatewayReady();
                onRegister?.Invoke();
            };

            host.Build(0, Gateways).Run(app.ApplicationServices);
            app.Use(async (context, next) =>
            {
                if (ConnectionCounter == null)
                {
                    ConnectionCounter = app.ApplicationServices.GetService<IConnectionCounter>();
                }

                ConnectionCounter.OnConnect();
                try
                {
                    if (await HttpHandler.Handle(app, context) == false)
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

        /// <summary>
        /// 获取客户端真实地址
        /// </summary>
        /// <param name="trustXForwardedFor">受信任的X-Forwarded-For节点地址
        /// 这个地址应该包括webapi、nginx等反向代理的ip地址
        /// </param>
        /// <returns></returns>
        public static string GetRemoteIpAddress(this HttpContext httpContext , string[] trustXForwardedFor)
        {
            var remoteIpAddr =  httpContext.Connection.RemoteIpAddress.ToString();

            if (trustXForwardedFor != null && trustXForwardedFor.Length > 0 && httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues x_for))
            {
                var x_forArr = x_for.ToString().Split(',').Select(m => m.Trim()).Where(m => m.Length > 0).ToArray();
                for (int i = 0; i < x_forArr.Length; i++)
                {
                    var ip = x_forArr[i];
                    if (trustXForwardedFor.Contains(ip) && i > 0)
                        return x_forArr[i - 1];
                }

            }

            return remoteIpAddr;
        }
    }
}
