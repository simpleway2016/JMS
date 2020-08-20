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
        /// <param name="authorizationContentType">token的类型</param>
        /// <param name="serverAddress">token服务器地址</param>
        /// <param name="serverPort">token服务器端口</param>
        /// <param name="headerName">客户端通过哪个头部传递token</param>
        /// <param name="cert">访问token服务器的证书</param>
        public static void AddJmsTokenAuthentication(this IServiceCollection services, AuthorizationContentType authorizationContentType, string serverAddress, int serverPort, string headerName = "Authorization", X509Certificate2 cert = null)
        {
            MyAuthHandler.HeaderName = headerName;
            MyAuthHandler.ServerAddress = serverAddress;
            MyAuthHandler.ServerPort = serverPort;
            MyAuthHandler.Cert = cert;
            MyAuthHandler.AuthorizationContentType = authorizationContentType;

            services.AddAuthentication(options =>
            {

                options.AddScheme<MyAuthHandler>(MyAuthHandler.SchemeName, "default scheme");
                options.DefaultAuthenticateScheme = MyAuthHandler.SchemeName;
                options.DefaultChallengeScheme = MyAuthHandler.SchemeName;
            });
        }
    }
}
