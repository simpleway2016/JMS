using JMS;
using JMS.Token;
using JMS.Token.AspNetCore;
using Microsoft.AspNetCore.Authentication;
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
        /// 使用JMS.Token作为身份验证
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serverAddress">token服务器地址,null表示不需要服务器，本地自行验证</param>
        /// <param name="headerNames">客户端通过哪个头部传递token，可以同时定义多个头，如 ['Authorization','Sec-WebSocket-Protocol']，任何一个头验证通过都是可以的</param>
        /// <param name="authCallback">验证通过后，回调的函数，callback如果返回false，表示验证失败
        /// 三个参数分别是 token content ticket
        /// </param>
        public static AuthenticationBuilder AddJmsTokenAspNetCore(this IServiceCollection services, NetAddress serverAddress, string[] headerNames, Func<AuthenticationParameter, bool> authCallback = null)
        {
            AuthHandler.HeaderNames = headerNames;
            AuthHandler.ServerAddress = serverAddress;
            AuthHandler.Callback = authCallback;

            services.AddSingleton<TokenClient>(p => new TokenClient(serverAddress));

            return services.AddAuthentication(options =>
            {

                options.AddScheme<AuthHandler>(AuthHandler.SchemeName, "default scheme");
                options.DefaultAuthenticateScheme = AuthHandler.SchemeName;
                options.DefaultChallengeScheme = AuthHandler.SchemeName;
            });
        }
    }
}
