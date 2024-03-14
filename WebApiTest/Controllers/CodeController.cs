using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace WebApiTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CodeController : ControllerBase
    {

        [HttpGet]
        public string Get(string service,bool? isJs)
        {
            if (isJs == true)
            {
                using ( var client = new JMS.RemoteClient("127.0.0.1", 8911))
                {
                    var serviceObj = client.TryGetMicroService(service);
                    var json = serviceObj.GetServiceInfo();

                    var js = new GenerateCode.GenerateRedirectCodeJavaScript(json).Build();
                    return js;
                }
            }
            else
            {
                using (var client = new JMS.RemoteClient("127.0.0.1", 8911))
                {
                    var serviceObj = client.TryGetMicroService(service);
                    var json = serviceObj.GetServiceInfo();

                    var ts = new GenerateCode.GenerateRedirectCodeTypeScript(json).Build();
                    return ts;
                }
            }
        }
    }
}
