using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JMSTokenTestWeb.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]/[action]")]
    public class HomeController : ControllerBase
    {
       
        [HttpGet]
        public string Test()
        {
            return this.User.FindFirstValue("Content");
        }
    }
}
