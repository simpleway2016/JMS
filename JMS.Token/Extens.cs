using JMS;
using JMS.Token;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class Extens
    {
        /// <summary>
        /// 使用JMS.Token进行身份验证（此方法在微服务端有效）
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serverAddress">token服务器地址</param>
        /// <param name="headerName">客户端通过哪个头部传递token</param>
        /// <param name="authCallback">验证回调</param>
        /// <returns></returns>
        public static IServiceCollection AddJmsTokenAuthentication(this IServiceCollection services,  NetAddress serverAddress, string headerName = "Authorization", Func<AuthenticationParameter,  bool>  authCallback = null)
        {
            return AddJmsTokenAuthentication(services, serverAddress, new string[] { headerName }, authCallback);
        }

        /// <summary>
        /// 使用JMS.Token进行身份验证（此方法在微服务端有效）
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serverAddress">token服务器地址</param>
        /// <param name="headerNames">客户端通过哪个头部传递token，可以同时定义多个头，如 ['Authorization','Sec-WebSocket-Protocol']，任何一个头验证通过都是可以的</param>
        /// <param name="authCallback">验证回调</param>
        /// <returns></returns>
        public static IServiceCollection AddJmsTokenAuthentication(this IServiceCollection services, NetAddress serverAddress, string[] headerNames, Func<AuthenticationParameter, bool> authCallback = null)
        {
            if (headerNames == null)
                headerNames = new string[] { "Authorization" };

            AuthenticationHandler.HeaderNames = headerNames;
            AuthenticationHandler.ServerAddress = serverAddress;

            AuthenticationHandler.Callback = authCallback;

            services.AddSingleton<IAuthenticationHandler, AuthenticationHandler>();
            services.AddSingleton<TokenClient>(p => {
                if (TokenClient.Logger == null)
                    TokenClient.Logger = p.GetService<ILogger<TokenClient>>();

                var client = new TokenClient(serverAddress);
                return client;
            });

            return services;
        }
    }
}
