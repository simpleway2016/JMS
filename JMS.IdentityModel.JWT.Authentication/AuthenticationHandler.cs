using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.IdentityModel.JWT.Authentication
{
    public class AuthenticationParameter
    {
        /// <summary>
        /// 待验证的token
        /// </summary>
        public string Token { get; }
        /// <summary>
        /// token解码后内容，可在回调中修改
        /// </summary>
        public ClaimsPrincipal Content { get; set; }
        public AuthenticationParameter(string token)
        {
            this.Token = token;
        }
    }
    class AuthenticationHandler : IAuthenticationHandler
    {
        static ILogger<AuthenticationHandler> _logger;
        public static Func<AuthenticationParameter, bool> Callback;
        public static string[] HeaderNames;
        public static string ServerUrl;
        
        public static int RefreshPublicKeySeconds;
        static RsaSecurityKey rsaSecurityKey;
        public AuthenticationHandler(ILogger<AuthenticationHandler> logger)
        {
            _logger = logger;

        }

        internal static async void GetPublicKeyAsync()
        {
            while(true)
            {
                try
                {
                    if (ServerUrl.EndsWith("/") == false)
                        ServerUrl += "/";
                  
                    rsaSecurityKey = IdentityModelHelper.GetSecurityKey(ServerUrl);

                    await Task.Delay(RefreshPublicKeySeconds*1000);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "");
                    await Task.Delay(1000);
                    continue;
                }
            }
        }

        public ClaimsPrincipal Authenticate(IDictionary<string, string> headers)
        {
            string token = null;
            foreach (var header in HeaderNames)
            {
                if (headers.TryGetValue(header,out token) && !string.IsNullOrEmpty(token))
                {
                    break;
                }
            }

            return VerifyToken(token);
        }

        public ClaimsPrincipal VerifyToken(string token)
        {
            for (int i = 0; i < 8 && rsaSecurityKey == null; i++)
            {
                Thread.Sleep(1000);
            }
            if(rsaSecurityKey == null)
            {
                throw new AuthenticationException("miss signature key");
            }
            if (string.IsNullOrEmpty(token))
                throw new AuthenticationException("Authentication failed");

            try
            {
                if (token.StartsWith("Bearer "))
                {
                    token = token.Substring(7);
                }

                var ret = JwtHelper.Authenticate(rsaSecurityKey, token);

                if (Callback != null)
                {
                    AuthenticationParameter authParameter = new AuthenticationParameter(token);
                    authParameter.Content = ret;
                    if (!Callback(authParameter))
                    {
                        _logger?.LogDebug("身份验证回调处理不通过");
                        throw new AuthenticationException("Authentication failed");
                    }
                    return authParameter.Content;
                }
                return ret;
            }
            catch (AuthenticationException ex)
            {
                _logger?.LogDebug("身份验证发生异常:{0}", ex.Message);
                if (Callback != null)
                {
                    AuthenticationParameter authParameter = new AuthenticationParameter(token);
                    if (Callback(authParameter))
                    {
                        return authParameter.Content;
                    }
                }

                throw ex;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("身份验证发生异常:{0}", ex.Message);
                if (Callback != null)
                {
                    AuthenticationParameter authParameter = new AuthenticationParameter(token);
                    if (Callback(authParameter))
                    {
                        return authParameter.Content;
                    }
                }
                throw new AuthenticationException("Authentication failed");
            }
        }
    }
}
