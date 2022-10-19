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
using System.Net.WebSockets;
using System.Text;
using Way.Lib;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class JmsRetryHandler
    {
        public static bool Handle(IApplicationBuilder app, HttpContext context)
        {
            using (var wsClient = new WSClient(context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false).GetAwaiter().GetResult()))
            {
                var tranId = wsClient.ReceiveData();
                var apiRetry = app.ApplicationServices.GetService<ApiRetryCommitMission>();
                try
                {
                    int ret = apiRetry.RetryTranaction(context, tranId);
                    wsClient.SendData(new InvokeResult { Success = true, Data = ret }.ToJsonString());
                }
                catch (Exception ex)
                {
                    wsClient.SendData(new InvokeResult
                    {
                        Success = false,
                        Error = ex.Message

                    }.ToJsonString());
                  
                }
                wsClient.Close(WebSocketCloseStatus.NormalClosure, null);
                return true;
            }
        }
    }
}
