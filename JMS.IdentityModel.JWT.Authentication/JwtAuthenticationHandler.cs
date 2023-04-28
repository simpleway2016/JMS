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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS.IdentityModel.JWT.Authentication
{

    class JwtAuthenticationHandler : IAuthenticationHandler
    {
        static ILogger<JwtAuthenticationHandler> _logger;
        public static Func<AuthenticationParameter, bool> Callback;
        public static string[] HeaderNames;
        public static string JwtKey;
        public JwtAuthenticationHandler(ILogger<JwtAuthenticationHandler> logger)
        {
            _logger = logger;

        }


        public object Authenticate(IDictionary<string, string> headers)
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

        public object VerifyToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                throw new AuthenticationException("Authentication failed");

            try
            {
                if (token.StartsWith("Bearer "))
                {
                    token = token.Substring(7);
                }

                if (token.Contains("_"))
                    token = token.Replace("_", "=");
                if (token.Contains("-"))
                    token = token.Replace("-", "/");

                var ret = JwtHelper.Authenticate(JwtKey, token);

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
