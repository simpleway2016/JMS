using JMS.Token;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Claims;

namespace JMS
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
        public static Func<AuthenticationParameter, bool> Callback;
        public static string[] HeaderNames;
        public static NetAddress ServerAddress;

        public AuthenticationHandler(ILogger<TokenClient> logger)
        {
            TokenClient.Logger = logger;
        }

        public ClaimsPrincipal Authenticate(IDictionary<string, string> headers)
        {
            string token = null;
            foreach (var header in HeaderNames)
            {
                if (headers.TryGetValue(header, out string val))
                {
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        token = val;
                        break;
                    }
                }
            }

            return VerifyToken(token);
        }

        public ClaimsPrincipal VerifyToken(string token)
        {
            if (token == null)
            {
                throw new AuthenticationException("Authentication failed, no token.");
            }
            TokenClient client = new TokenClient(ServerAddress);

            try
            {
                var ret = client.Verify(token);

                if (Callback != null)
                {
                    AuthenticationParameter authParameter = new AuthenticationParameter(token);
                    authParameter.Content = ret;
                    if (!Callback(authParameter))
                    {
                        throw new AuthenticationException("Authentication failed");
                    }
                    return authParameter.Content;
                }
                return ret;
            }
            catch (AuthenticationException e)
            {
                if (Callback != null)
                {
                    AuthenticationParameter authParameter = new AuthenticationParameter(token);
                    if (Callback(authParameter))
                    {
                        return authParameter.Content;
                    }
                }

                throw e;
            }
            catch
            {
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
