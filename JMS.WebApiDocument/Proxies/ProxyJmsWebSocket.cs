using JMS.Dtos;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.WebApiDocument.Proxies
{
    internal class ProxyJmsWebSocket
    {
        internal static async Task Run(ClientServiceDetail location, HttpContext context, string method)
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

            using (NetClient proxyClient = new NetClient())
            {
                await proxyClient.ConnectAsync(new NetAddress(hostUri.Host, hostUri.Port));
                if (location.UseSsl)
                {
                    await proxyClient.AsSSLClientAsync(hostUri.Host, null, System.Security.Authentication.SslProtocols.None, (sender, certificate, chain, sslPolicyErrors) => true);
                }

                StringBuilder strBuffer = new StringBuilder();
                var path = context.Request.GetEncodedPathAndQuery();
                var index = path.IndexOf("/JMSRedirect/");
                path = path.Substring(index + 12);
                strBuffer.Append($"GET {path} HTTP/1.1\r\n");

                var ip = context.Connection.RemoteIpAddress.ToString();
                string strForwared = null;
                if (context.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues forwardedFor))
                {
                    context.Request.Headers.Remove("X-Forwarded-For");
                    strForwared = $"{forwardedFor}, {ip}";

                }
                else
                {
                    strForwared = ip;
                }
                foreach (var pair in context.Request.Headers)
                {
                    strBuffer.Append($"{pair.Key}: {pair.Value.ToString()}\r\n");

                }

                strBuffer.Append($"X-Forwarded-For: {strForwared}\r\n");
                strBuffer.Append("\r\n");
                var data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部到服务器
                proxyClient.Write(data);

                var connectionTransportFeature = context.Features.Get<IConnectionTransportFeature>();
                var input = connectionTransportFeature.Transport.Input;
                var output = connectionTransportFeature.Transport.Output;

                proxyClient.ReadTimeout = 0;

                _ = proxyClient.ReadAndSend(output);

                await input.ReadAndSend(proxyClient);
            }
        }
    }
}
