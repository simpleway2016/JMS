using JMS.Dtos;
using JMS.ServerCore;
using JMS.ServerCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Applications.HttpMiddlewares
{
    internal class WebSocketMiddleware : IHttpMiddleware
    {
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IDictionary<string, string> headers)
        {
            if (headers.TryGetValue("Connection", out string connection)
               && string.Equals(connection, "Upgrade", StringComparison.OrdinalIgnoreCase)
               && headers.TryGetValue("Upgrade", out string upgrade)
               && string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
            {
                var servieName = requestPath.Substring(1);
                if (servieName.Contains("/"))
                {
                    servieName = servieName.Substring(0, servieName.IndexOf("/"));
                }
                if (servieName.Contains("?"))
                {
                    servieName = servieName.Substring(0, servieName.IndexOf("?"));
                }

                using var rc = new RemoteClient(WebApiProgram.GatewayAddresses);
                var service = await rc.TryGetMicroServiceAsync(servieName);
                if (service == null || service.ServiceLocation.AllowGatewayProxy == false)
                {
                    client.OutputHttpNotFund();
                    return true;
                }

                if (service.ServiceLocation.Type == ServiceType.WebApi)
                {
                    //去除servicename去代理访问
                    requestPath = requestPath.Substring(servieName.Length + 1);
                    if (requestPath.Length == 0)
                        requestPath = "/";
                }

                Uri hostUri = null;
                if (service.ServiceLocation.ServiceAddress.Contains("://"))
                {
                    hostUri = new Uri(service.ServiceLocation.ServiceAddress.ToLower());
                }
                else if (service.ServiceLocation.UseSsl)
                {
                    hostUri = new Uri($"wss://{service.ServiceLocation.ServiceAddress}:{service.ServiceLocation.Port}");
                }
                else
                {
                    hostUri = new Uri($"ws://{service.ServiceLocation.ServiceAddress}:{service.ServiceLocation.Port}");
                }


                using NetClient proxyClient = new NetClient();
                await proxyClient.ConnectAsync(new NetAddress(hostUri.Host, hostUri.Port));
                if (hostUri.Scheme == "https" || hostUri.Scheme == "wss")
                {
                    await proxyClient.AsSSLClientAsync(hostUri.Host, null, System.Security.Authentication.SslProtocols.None, (sender, certificate, chain, sslPolicyErrors) => true);
                }

                StringBuilder strBuffer = new StringBuilder();

                Uri gatewayUri = new Uri($"http://{headers["Host"]}");

                strBuffer.AppendLine($"{httpMethod} {requestPath} HTTP/1.1");

                foreach (var pair in headers)
                {
                    if (pair.Key == "Host")
                    {
                        strBuffer.AppendLine($"Host: {hostUri.Host}");
                    }
                    else if (pair.Key == "Origin")
                    {
                        try
                        {
                            var uri = new Uri(pair.Value);
                            if (uri.Host == gatewayUri.Host)
                            {
                                strBuffer.AppendLine($"{pair.Key}: {uri.Scheme}://{hostUri.Authority}{uri.PathAndQuery}");
                            }
                            else
                            {
                                strBuffer.AppendLine($"{pair.Key}: {pair.Value}");
                            }
                        }
                        catch
                        {
                            strBuffer.AppendLine($"{pair.Key}: {pair.Value}");
                        }
                    }
                    else
                    {
                        strBuffer.AppendLine($"{pair.Key}: {pair.Value}");
                    }
                }

                strBuffer.AppendLine("");
                var data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部到服务器
                proxyClient.Write(data);

               HttpHelper.ReadAndSendForLoop(proxyClient, client);

                await HttpHelper.ReadAndSendForLoop(client, proxyClient);

                return true;
            }
            return false;
        }
    }
}
