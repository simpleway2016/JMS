using JMS;
using JMS.IdentityModel.JWT.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class Extens
    {
        /// <summary>
        /// 使用IdentityServer校验jwt token
        /// 如果验证后，要完全还原Claims各项，建议设置静态变量 JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        /// </summary>
        /// <param name="services"></param>
        /// <param name="identityServerUrl">IdentityServer服务器url</param>
        /// <param name="refreshPublicKeySeconds">多少秒和IdentityServer服务器同步一次公钥</param>
        /// <param name="headerName">客户端通过哪个头部传递token</param>
        /// <param name="authCallback">验证回调</param>
        /// <returns></returns>
        [Obsolete("use UseIdentityServerAndJwtAuthentication", false)]
        public static IServiceCollection UseIdentityServerTokenAuthentication(this IServiceCollection services,  string identityServerUrl,int refreshPublicKeySeconds = 60, string headerName = "Authorization", Func<AuthenticationParameter,  bool>  authCallback = null)
        {
            AuthenticationHandler.HeaderNames = new string[] { headerName};
            AuthenticationHandler.RefreshPublicKeySeconds = refreshPublicKeySeconds;
            AuthenticationHandler.Callback = authCallback;
            if (AuthenticationHandler.ServerUrl == null)
            {
                AuthenticationHandler.ServerUrl = identityServerUrl;
                new Thread(AuthenticationHandler.GetPublicKey).Start();
            }
            else
            {
                AuthenticationHandler.ServerUrl = identityServerUrl;
            }
            services.AddSingleton<IAuthenticationHandler, AuthenticationHandler>();

            return services;
        }

        /// <summary>
        /// 使用IdentityServer校验jwt token
        /// </summary>
        /// <param name="services"></param>
        /// <param name="identityServerUrl">IdentityServer服务器url</param>
        /// <param name="refreshPublicKeySeconds">多少秒和IdentityServer服务器同步一次公钥</param>
        /// <param name="headerNames">客户端通过哪些头部传递token，默认：Authorization</param>
        /// <param name="authCallback">验证回调</param>
        /// <returns></returns>
        public static IServiceCollection UseIdentityServerAndJwtAuthentication(this IServiceCollection services, string identityServerUrl, int refreshPublicKeySeconds = 60, string[] headerNames = null, Func<AuthenticationParameter, bool> authCallback = null)
        {
            if(headerNames == null)
            {
                headerNames = new string[] { "Authorization" };
            }
            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
            AuthenticationHandler.HeaderNames = headerNames;
            AuthenticationHandler.RefreshPublicKeySeconds = refreshPublicKeySeconds;
            AuthenticationHandler.Callback = authCallback;
            if (AuthenticationHandler.ServerUrl == null)
            {
                AuthenticationHandler.ServerUrl = identityServerUrl;
                new Thread(AuthenticationHandler.GetPublicKey).Start();
            }
            else
            {
                AuthenticationHandler.ServerUrl = identityServerUrl;
            }
            services.AddSingleton<IAuthenticationHandler, AuthenticationHandler>();

            return services;
        }

        /// <summary>
        /// 启用JWT Token身份验证
        /// 
        /// </summary>
        /// <param name="services"></param>
        /// <param name="jwtSecretKey">生成token的私钥</param>
        /// <param name="headerNames">客户端通过哪些头部传递token，默认：Authorization</param>
        /// <param name="authCallback">验证回调</param>
        /// <returns></returns>
        public static IServiceCollection UseJwtTokenAuthentication(this IServiceCollection services, string jwtSecretKey,  string[] headerNames = null, Func<AuthenticationParameter, bool> authCallback = null)
        {
            if (headerNames == null)
            {
                headerNames = new string[] { "Authorization" };
            }
            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
            JwtAuthenticationHandler.HeaderNames = headerNames;
            JwtAuthenticationHandler.Callback = authCallback;
            JwtAuthenticationHandler.JwtKey = jwtSecretKey;
            services.AddSingleton<IAuthenticationHandler, JwtAuthenticationHandler>();

            return services;
        }
    }
}
