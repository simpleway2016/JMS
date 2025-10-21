using JMS.Common.Collections;
using JMS.Dtos;
using JMS.ServerCore;
using JMS.ServerCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Applications.HttpMiddlewares
{
    internal class WebSocketMiddleware : IHttpMiddleware
    {
        IServiceProviderAllocator _serviceProviderAllocator;
        public WebSocketMiddleware(IServiceProviderAllocator serviceProviderAllocator)
        {
            this._serviceProviderAllocator = serviceProviderAllocator;
        }
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IgnoreCaseDictionary headers)
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

                var location = _serviceProviderAllocator.Alloc(new GetServiceProviderRequest()
                {
                    ServiceName = serviceName,
                    IsGatewayProxy = true,
                    Header = headers,
                    ClientAddress = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString()
                });
                if (location == null)
                {
                    return true;
                }

                if (location.Type == ServiceType.WebApi)
                {
                    //去除servicename去代理访问
                    requestPath = requestPath.Substring(serviceName.Length + 1);
                    if (requestPath.Length == 0)
                        requestPath = "/";
                }

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

                await client.ReadAndSendForLoop( proxyClient);

                return true;
            }

            return false;
        }
    }
}
