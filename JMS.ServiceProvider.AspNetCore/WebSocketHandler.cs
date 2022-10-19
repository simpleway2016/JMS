using JMS.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Way.Lib;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class WebSocketHandler
    {
        
        public static bool Handle(IApplicationBuilder app, HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("Protocol", out StringValues value))
            {
                if (value.Contains("JmsService"))
                {
                    return JmsServiceHandler.Handle(app, context);
                }
                else if (value.Contains("JmsRetry"))
                {
                    return JmsServiceHandler.Handle(app, context);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

      
    }
}
