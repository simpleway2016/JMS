using JMS.Common.Collections;
using JMS.Dtos;
using JMS.ServerCore;
using JMS.ServerCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Applications.HttpMiddlewares
{
    internal class WebSocketMiddleware : IHttpMiddleware
    {
        private readonly IWebApiHostEnvironment _webApiEnvironment;
        ILogger _logger;
        public WebSocketMiddleware(IWebApiHostEnvironment webApiEnvironment,ILoggerFactory loggerFactory)
        {
            _webApiEnvironment = webApiEnvironment;
            _logger = loggerFactory.CreateLogger("Request");
        }
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IgnoreCaseDictionary headers)
        {
            if (headers.TryGetValue("Connection", out string connection)
               && string.Equals(connection, "Upgrade", StringComparison.OrdinalIgnoreCase)
               && headers.TryGetValue("Upgrade", out string upgrade)
               && string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
            {
                bool writeLogger = _logger.IsEnabled(LogLevel.Trace);

                if (writeLogger)
                {
                    _logger.LogTrace($"WebSocket Requesting: {requestPath}\r\n{headers.ToJsonString(true)}");
                }

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

                using var rc = new RemoteClient(_webApiEnvironment.Config.Current.Gateways);
                var service = await rc.TryGetMicroServiceAsync(serviceName);
                if (service == null || service.ServiceLocation.AllowGatewayProxy == false)
                {
                    if (writeLogger)
                    {
                        _logger.LogTrace($"miss service: {serviceName}");
                    }

                    client.OutputHttpNotFund();
                    return true;
                }

                if (writeLogger)
                {
                    _logger.LogTrace($"service name: {serviceName}  address: {service.ServiceLocation.ServiceAddress}");
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
                    strBuffer.Append($"{pair.Key}: {pair.Value}\r\n");

                }

                strBuffer.Append("\r\n");
                var data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部到服务器
                proxyClient.Write(data);

                client.ReadTimeout = 0;
                proxyClient.ReadTimeout = 0;

                _ = proxyClient.ReadAndSendForLoop(client);

                await client.ReadAndSendForLoop(proxyClient);

                return true;
            }
            return false;
        }
    }
}
