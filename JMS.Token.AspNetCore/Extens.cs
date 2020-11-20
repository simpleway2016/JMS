using JMS;
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
        /// <param name="tokenContentType">token的类型</param>
        /// <param name="serverAddress">token服务器地址</param>
        /// <param name="serverPort">token服务器端口</param>
        /// <param name="headerName">客户端通过哪个头部传递token</param>
        /// <param name="authCallback">验证通过后，回调的函数，callback如果返回false，表示验证失败</param>
        /// <param name="cert">访问token服务器的证书</param>
        public static AuthenticationBuilder AddJmsTokenAuthentication(this IServiceCollection services, TokenContentType tokenContentType, string serverAddress, int serverPort, string headerName = "Authorization", Func<string,bool> authCallback = null, X509Certificate2 cert = null)
        {
            MyAuthHandler.HeaderName = headerName;
            MyAuthHandler.ServerAddress = serverAddress;
            MyAuthHandler.ServerPort = serverPort;
            MyAuthHandler.Cert = cert;
            MyAuthHandler.Callback = authCallback;
            MyAuthHandler.AuthorizationContentType = tokenContentType;

            return services.AddAuthentication(options =>
            {

                options.AddScheme<MyAuthHandler>(MyAuthHandler.SchemeName, "default scheme");
                options.DefaultAuthenticateScheme = MyAuthHandler.SchemeName;
                options.DefaultChallengeScheme = MyAuthHandler.SchemeName;
            });
        }
    }
}
