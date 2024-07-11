using JMS.Dtos;
using JMS.ServerCore;
using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.WebApiDocument
{
    internal class RequestHandler
    {
        public static Func<RemoteClient> ClientProviderFunc;

        /// <summary>
        /// 调用微服务方法
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="context"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        internal static async Task InvokeServiceMethod(string serviceName, HttpContext context, string method)
        {
            using (var client = ClientProviderFunc())
            {
                var service = await client.GetMicroServiceAsync(serviceName);

                if (service.ServiceLocation.AllowGatewayProxy == false)
                {
                    //不允许反向代理
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("not found");
                }
                if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.WebSocket)
                {
                    await Proxies.ProxyJmsWebSocket.Run(service.ServiceLocation, context, method);
                }
                else if (service.ServiceLocation.Type == ServiceType.WebApi)
                {
                    await Proxies.ProxyWebApi.Run(service.ServiceLocation, context, method);
                }
                else
                {
                    await Proxies.ProxyJmsService.Run(service, context, method);
                }


            }
        }
    }
}
