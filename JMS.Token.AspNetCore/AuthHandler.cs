using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Token.AspNetCore
{

    class AuthHandler : Microsoft.AspNetCore.Authentication.IAuthenticationHandler
    {
        public const string SchemeName = "JMS.Token";
        AuthenticationScheme _scheme;
        HttpContext _context;
        public static string[] HeaderNames;
        public static Func<AuthenticationParameter, bool> Callback;
        public static NetAddress ServerAddress;

        public AuthHandler(ILogger<TokenClient> logger)
        {
            TokenClient.Logger = logger;
        }

        /// <summary>
        /// 初始化认证
        /// </summary>
        public Task InitializeAsync(AuthenticationScheme scheme,HttpContext context)
        {
            _scheme = scheme;
            _context = context;           
            return Task.CompletedTask;
        }

        /// <summary>
        /// 认证处理
        /// </summary>
        public Task<AuthenticateResult> AuthenticateAsync()
        {
            string token = null;
            foreach( var header in HeaderNames)
            {
                if (_context.Request.Headers.TryGetValue(header,out StringValues values))
                {
                    string val = values.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        token = val;
                        break;
                    }
                }
            }
            if (token == null)
            {
                return Task.FromResult(AuthenticateResult.Fail(""));
            }
           
            TokenClient client = new TokenClient(ServerAddress);
            try
            {
                var ret = client.Verify(token);
                
                if(Callback != null)
                {
                    AuthenticationParameter parameter = new AuthenticationParameter(token);
                    parameter.Content = ret;
                    if (!Callback(parameter))
                    {
                        return Task.FromResult(AuthenticateResult.Fail(""));
                    }
                    else
                    {
                        return Task.FromResult(AuthenticateResult.Success(GetAuthTicket(parameter.Content)));
                    }
                }

                return Task.FromResult(AuthenticateResult.Success(GetAuthTicket(ret)));
            }
            catch(AuthenticationException ex)
            {
                if (Callback != null)
                {
                    AuthenticationParameter parameter = new AuthenticationParameter(token);
                    if (Callback(parameter))
                    {
                        return Task.FromResult(AuthenticateResult.Success(GetAuthTicket(parameter.Content)));
                    }
                }

                return Task.FromResult(AuthenticateResult.Fail(ex.Message));
            }
            catch
            {
                if (Callback != null)
                {
                    AuthenticationParameter parameter = new AuthenticationParameter(token);
                    if (Callback(parameter))
                    {
                        return Task.FromResult(AuthenticateResult.Success(GetAuthTicket(parameter.Content)));
                    }
                }

                return Task.FromResult(AuthenticateResult.Fail(""));
            }            
        }

        AuthenticationTicket GetAuthTicket(TokenContent tokenContent)
        {
            if (tokenContent == null)
                tokenContent = new TokenContent();

               var claimsIdentity = new ClaimsIdentity(new Claim[]
            {
                new Claim("Content", tokenContent.Content.ToString()),
                new Claim(ClaimTypes.NameIdentifier , tokenContent.Content.ToString()),
                new Claim(ClaimTypes.Role , tokenContent.Role),
            }, "JMS.Token"); ;

            var principal = new ClaimsPrincipal(claimsIdentity);
            return new AuthenticationTicket(principal, _scheme.Name);
        }

        /// <summary>
        /// 权限不足时的处理
        /// </summary>
        public Task ForbidAsync(AuthenticationProperties properties)
        {
            _context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 未登录时的处理
        /// </summary>
        public Task ChallengeAsync(AuthenticationProperties properties)
        {
            _context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return Task.CompletedTask;
        }
    }
}
