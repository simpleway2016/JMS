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

namespace WebApiTest
{

    class MyAuthHandler : Microsoft.AspNetCore.Authentication.IAuthenticationHandler
    {
        public const string SchemeName = "JMS.Token";

        HttpContext _context;
        /// <summary>
        /// 初始化认证
        /// </summary>
        public Task InitializeAsync(AuthenticationScheme scheme,HttpContext context)
        {
            _context = context;           
            return Task.CompletedTask;
        }

        /// <summary>
        /// 认证处理
        /// </summary>
        public Task<AuthenticateResult> AuthenticateAsync()
        {
            return Task.FromResult(AuthenticateResult.Success(GetAuthTicket("abc")));
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
            return new AuthenticationTicket(principal, SchemeName);
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
