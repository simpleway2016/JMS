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
        private readonly IWebApiEnvironment _webApiEnvironment;

        public WebSocketMiddleware(IWebApiEnvironment webApiEnvironment)
        {
            _webApiEnvironment = webApiEnvironment;
        }
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IDictionary<string, string> headers)
        {
            if (headers.TryGetValue("Connection", out string connection)
               && string.Equals(connection, "Upgrade", StringComparison.OrdinalIgnoreCase)
               && headers.TryGetValue("Upgrade", out string upgrade)
               && string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
            {
                int indexflag;
                var serviceName = requestPath.Substring(1);
                if ((indexflag = serviceName.IndexOf('/')) > 0)
                {
                    serviceName = serviceName.Substring(0, indexflag);
                }

                if (serviceName.Contains("?"))
                {
                    serviceName = serviceName.Substring(0, serviceName.IndexOf("?"));
                }

                using var rc = new RemoteClient(_webApiEnvironment.GatewayAddresses);
                var service = await rc.TryGetMicroServiceAsync(serviceName);
                if (service == null || service.ServiceLocation.AllowGatewayProxy == false)
                {
                    client.OutputHttpNotFund();
                    return true;
                }

                if (service.ServiceLocation.Type == ServiceType.WebApi)
                {
                    //去除servicename去代理访问
                    requestPath = requestPath.Substring(serviceName.Length + 1);
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

                strBuffer.Append($"{httpMethod} {requestPath} HTTP/1.1\r\n");

                foreach (var pair in headers)
                {
                    if (pair.Key == "Host")
                    {
                        strBuffer.Append($"Host: {hostUri.Host}\r\n");
                    }
                    else if (pair.Key == "Origin")
                    {
                        try
                        {
                            var uri = new Uri(pair.Value);
                            if (uri.Host == gatewayUri.Host)
                            {
                                strBuffer.Append($"{pair.Key}: {uri.Scheme}://{hostUri.Authority}{uri.PathAndQuery}\r\n");
                            }
                            else
                            {
                                strBuffer.Append($"{pair.Key}: {pair.Value}\r\n");
                            }
                        }
                        catch
                        {
                            strBuffer.Append($"{pair.Key}: {pair.Value}\r\n");
                        }
                    }
                    else
                    {
                        strBuffer.Append($"{pair.Key}: {pair.Value}\r\n");
                    }
                }

                strBuffer.Append("\r\n");
                var data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部到服务器
                proxyClient.Write(data);

                client.ReadTimeout = 0;
                proxyClient.ReadTimeout = 0;

                proxyClient.ReadAndSendForLoop( client);

                await client.ReadAndSendForLoop(proxyClient);

                return true;
            }
            return false;
        }
    }
}
