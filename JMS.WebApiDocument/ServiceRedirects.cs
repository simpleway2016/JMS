using JMS.Dtos;
using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.WebApiDocument
{
    internal class ServiceRedirects
    {
        public static ServiceRedirectConfig[] Configs;
        public static Func<RemoteClient> ClientProviderFunc;

        internal static async Task InvokeWebSocket(ClientServiceDetail location, HttpContext context, string method, string[] redirectHeaders)
        {

            Uri hostUri = null;
            if (location.ServiceAddress.Contains("://"))
            {
                hostUri = new Uri(location.ServiceAddress.ToLower());
            }
            else if (location.UseSsl)
            {
                hostUri = new Uri($"wss://{location.ServiceAddress}:{location.Port}");
            }
            else
            {
                hostUri = new Uri($"ws://{location.ServiceAddress}:{location.Port}");
            }

            var websocket = await context.WebSockets.AcceptWebSocketAsync();
            ClientWebSocket proxyWebSocket = new ClientWebSocket();
            foreach (var header in context.Request.Headers)
            {
                proxyWebSocket.Options.SetRequestHeader(header.Key, header.Value.ToString());
            }

            await proxyWebSocket.ConnectAsync(hostUri, CancellationToken.None);
            ReadSend(websocket, proxyWebSocket);

            await ReadSend(proxyWebSocket, websocket);
        }

        static async Task ReadSend(WebSocket reader,WebSocket sender)
        {
            byte[] data = ArrayPool<byte>.Shared.Rent(40960);
            try
            {
                var buffer = new ArraySegment<byte>(data);
                while (true)
                {
                    var ret = await reader.ReceiveAsync(buffer, CancellationToken.None);
                    if (ret.CloseStatus != null || reader.State != WebSocketState.Open)
                    {
                        if(ret.CloseStatus != null && sender.State == WebSocketState.Open)
                        {
                            await sender.CloseAsync(ret.CloseStatus.Value, ret.CloseStatusDescription, CancellationToken.None);
                        }
                        return;
                    }
                    else if(ret.Count > 0)
                    {
                        await sender.SendAsync(buffer.Slice(0 , ret.Count), ret.MessageType, ret.EndOfMessage, CancellationToken.None);
                    }
                }
            }
            catch (Exception)
            {
                
            }
            finally
            {
                reader.Dispose();
                sender.Dispose();
                ArrayPool<byte>.Shared.Return(data);
            }
        }

        /// <summary>
        /// 调用微服务方法
        /// </summary>
        /// <param name="config"></param>
        /// <param name="context"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        internal static async Task<object> InvokeServiceMethod(ServiceRedirectConfig config,HttpContext context,string method, string[] redirectHeaders)
        {

            byte[] postContent = null;
            if (context.Request.ContentLength != null && context.Request.ContentLength > 0)
            {
                postContent = new byte[(int)context.Request.ContentLength];
                await context.Request.Body.ReadAsync(postContent, 0, postContent.Length);
            }

            object[] _parames = null;
            if (postContent != null)
            {
                var json = Encoding.UTF8.GetString(postContent);
                _parames = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(json);
            }
            else if (context.Request.Query.ContainsKey("params"))
            {
                var json = context.Request.Query["params"].ToString();
                _parames = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(json);
            }

            using (var client = ClientProviderFunc())
            {
                var service = await client.GetMicroServiceAsync(config.ServiceName);
                if( context.WebSockets.IsWebSocketRequest && service.ServiceLocation.Type == JMS.Dtos.ServiceType.WebSocket)
                {
                    config.Handled = true;
                    client.Dispose();
                    await InvokeWebSocket(service.ServiceLocation, context, method, redirectHeaders);                    
                    return null;
                }
                else if(service.ServiceLocation.Type == ServiceType.WebApi)
                {
                    throw new Exception("不支持从此处访问webapi类型的微服务");
                }
                if (redirectHeaders == null)
                {
                    foreach (var header in context.Request.Headers)
                    {
                        if (header.Key == "TranId")
                            continue;
                        else if (header.Key == "Tran")
                            continue;
                        else if (header.Key == "TranFlag")
                            continue;

                        client.SetHeader(header.Key, header.Value.ToString());
                    }
                }
                else
                {
                    foreach (var header in redirectHeaders)
                    {
                        if (header == "TranId")
                            continue;
                        else if (header == "Tran")
                            continue;
                        else if (header == "TranFlag")
                            continue;

                        if (context.Request.Headers.TryGetValue(header, out StringValues value))
                        {
                            client.SetHeader(header, value.ToString());
                        }
                    }
                }
                
               
                var ip = context.Connection.RemoteIpAddress.ToString();
                if (client.TryGetHeader("X-Forwarded-For", out string xff))
                {
                    if (xff.Contains(ip) == false)
                    {
                        xff += $", {ip}";
                        client.SetHeader("X-Forwarded-For", xff);
                    }
                }
                else
                {
                    client.SetHeader("X-Forwarded-For", ip);
                }

                
                if (_parames == null)
                {
                    return await service.InvokeAsync<object>(method);
                }
                else
                    return await service.InvokeAsync<object>(method, _parames);
            }
        }
    }
}
