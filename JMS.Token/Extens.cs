using JMS;
using JMS.Token;
using Microsoft.Extensions.DependencyInjection;
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
        public static IServiceCollection UseJmsTokenAuthentication(this IServiceCollection services,  NetAddress serverAddress, string headerName = "Authorization", Func<AuthenticationParameter,  bool>  authCallback = null)
        {
            AuthenticationHandler.HeaderName = headerName;
            AuthenticationHandler.ServerAddress = serverAddress;
          
            AuthenticationHandler.Callback = authCallback;

            services.AddSingleton<IAuthenticationHandler, AuthenticationHandler>();
            services.AddSingleton<TokenClient>(p => new TokenClient(serverAddress));

            return services;
        }
    }
}
