using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Token.AspNetCore
{
    public enum TokenContentType
    {
        /// <summary>
        /// long[]类型
        /// </summary>
        Longs = 1,
        /// <summary>
        /// 字符串内容
        /// </summary>
        String = 2
    }
    class MyAuthHandler : Microsoft.AspNetCore.Authentication.IAuthenticationHandler
    {
        public const string SchemeName = "JMS.Token";
        AuthenticationScheme _scheme;
        HttpContext _context;
        public static string HeaderName;
        public static string ServerAddress;
        public static int ServerPort;
        public static X509Certificate2 Cert;
        public static TokenContentType AuthorizationContentType;

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
                string strContent;
                if (AuthorizationContentType == TokenContentType.Longs)
                {
                    strContent = client.VerifyForLongs(token).ToJsonString();
                }
                else
                {
                    strContent = client.VerifyForString(token);
                }
                var ticket = GetAuthTicket(strContent);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch
            {
                return Task.FromResult(AuthenticateResult.Fail(""));
            }            
        }

        AuthenticationTicket GetAuthTicket(string content)
        {
            var claimsIdentity = new ClaimsIdentity(new Claim[]
            {
                new Claim("Content", content)
            }, "JMS.Token");

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
