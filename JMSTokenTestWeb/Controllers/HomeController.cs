using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using JMS.Token;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JMSTokenTestWeb.Controllers
{

    [ApiController]
    [Route("[controller]/[action]")]
    public class HomeController : ControllerBase
    {
        TokenClient _tokenClient;
        public HomeController(TokenClient tokenClient)
        {
            this._tokenClient = tokenClient;

        }

        [Authorize]
        [HttpGet]
        public string GetUserName()
        {
            return this.User.FindFirstValue("Content");
        }


        [Authorize]
        [HttpGet]
        public void Logout()
        {
            var token = Request.Headers["Authorization"].ToString();
            _tokenClient.SetTokenDisable(token);
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="username"></param>
        /// <returns>返回token</returns>
        [HttpGet]
        public string Login(string username)
        {
            return _tokenClient.Build(username, DateTime.Now.AddMinutes(2));
        }
    }
}
