using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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

    class MyAuthHandler : Microsoft.AspNetCore.Authentication.IAuthenticationHandler
    {
        public const string SchemeName = "JMS.Token";
        AuthenticationScheme _scheme;
        HttpContext _context;
        public static string HeaderName;
        public static Func<AuthenticationParameter, bool> Callback;
        public static string ServerAddress;
        public static int ServerPort;
        public static X509Certificate2 Cert;

        public MyAuthHandler(ILogger<TokenClient> logger)
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
            if (_context.Request.Headers.ContainsKey(HeaderName) == false)
            {
                return Task.FromResult(AuthenticateResult.Fail(""));
            }

            var token = _context.Request.Headers[HeaderName].FirstOrDefault();
            TokenClient client = new TokenClient(ServerAddress,ServerPort , Cert);
            try
            {
                string strContent = client.Verify(token).ToString();
                
                if(Callback != null)
                {
                    AuthenticationParameter parameter = new AuthenticationParameter(token);
                    parameter.Content = strContent;
                    if (!Callback(parameter))
                    {
                        return Task.FromResult(AuthenticateResult.Fail(""));
                    }
                    else
                    {
                        return Task.FromResult(AuthenticateResult.Success(GetAuthTicket(parameter.Content?.ToString())));
                    }
                }

                return Task.FromResult(AuthenticateResult.Success(GetAuthTicket(strContent)));
            }
            catch(AuthenticationException ex)
            {
                if (Callback != null)
                {
                    AuthenticationParameter parameter = new AuthenticationParameter(token);
                    if (Callback(parameter))
                    {
                        return Task.FromResult(AuthenticateResult.Success(GetAuthTicket(parameter.Content?.ToString())));
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
                        return Task.FromResult(AuthenticateResult.Success(GetAuthTicket(parameter.Content?.ToString())));
                    }
                }

                return Task.FromResult(AuthenticateResult.Fail(""));
            }            
        }

        AuthenticationTicket GetAuthTicket(string content)
        {
            if (content == null)
                content = "";
               var claimsIdentity = new ClaimsIdentity(new Claim[]
            {
                new Claim("Content", content),
                new Claim(ClaimTypes.NameIdentifier , content)
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
