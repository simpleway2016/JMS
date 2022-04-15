using JMS;
using JMS.IdentityModel.JWT.Authentication;
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
        /// 使用IdentityServer校验jwt token
        /// </summary>
        /// <param name="services"></param>
        /// <param name="identityServerUrl">IdentityServer服务器url</param>
        /// <param name="refreshPublicKeySeconds">多少秒和IdentityServer服务器同步一次公钥</param>
        /// <param name="headerName">客户端通过哪个头部传递token</param>
        /// <param name="authCallback">验证回调</param>
        /// <returns></returns>
        public static IServiceCollection UseIdentityServerTokenAuthentication(this IServiceCollection services,  string identityServerUrl,int refreshPublicKeySeconds = 60, string headerName = "Authorization", Func<AuthenticationParameter,  bool>  authCallback = null)
        {
            AuthenticationHandler.HeaderName = headerName;
            AuthenticationHandler.ServerUrl = identityServerUrl;
            AuthenticationHandler.RefreshPublicKeySeconds = refreshPublicKeySeconds;
            AuthenticationHandler.Callback = authCallback;

            services.AddSingleton<IAuthenticationHandler, AuthenticationHandler>();

            return services;
        }
    }
}
