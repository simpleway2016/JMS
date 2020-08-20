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
    
    [ApiController]
    [Route("[controller]/[action]")]
    public class WeatherForecastController : ControllerBase
    {
        [Authorize]
        [HttpGet]
        public string Test()
        {
            return this.User.FindFirstValue("Content");
        }
    }
}
