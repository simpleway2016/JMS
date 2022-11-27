using JMS.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class JmsRetryHandler
    {
        public static async Task<bool> Handle(IApplicationBuilder app, HttpContext context)
        {
            using (var netClient = new NetClient(new ConnectionStream(context)))
            {
                netClient.KeepAlive = true;
                var arr = context.Request.Path.Value.Substring(1).Split(',');
                var tranId = arr[0];
                string tranFlag = null;
                if (arr.Length > 1)
                    tranFlag = arr[1]; 


                var apiRetry = app.ApplicationServices.GetService<ApiRetryCommitMission>();
                try
                {
                    int ret = apiRetry.RetryTranaction(context, tranId , tranFlag);
                    netClient.WriteServiceData(new InvokeResult { Success = true, Data = ret });
                }
                catch (Exception ex)
                {
                    netClient.WriteServiceData(new InvokeResult
                    {
                        Success = false,
                        Error = ex.Message

                    });
                  
                }
                releaseNetClient(netClient);
                return true;
            }
        }

        static void releaseNetClient(NetClient client)
        {
            client.Socket = null;
            client.InnerStream = null;
        }
    }
}
