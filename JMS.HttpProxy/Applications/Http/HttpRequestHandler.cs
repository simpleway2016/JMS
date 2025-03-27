
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Web;
using Microsoft.CodeAnalysis;
using System.Reflection.PortableExecutable;
using System.Net;
using System.Buffers;
using static System.Runtime.InteropServices.JavaScript.JSType;
using JMS.ServerCore;
using Microsoft.Extensions.Configuration;
using JMS.HttpProxy.Servers;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.X509;

namespace JMS.HttpProxy.Applications.Http
{
    /// <summary>
    /// 处理http请求
    /// </summary>
    class HttpRequestHandler
    {
        private readonly RequestTimeLimter _requestTimeLimter;
        private readonly StaticFilesRequestHandler _staticFilesRequestHandler;
        private readonly NetClientProviderFactory _netClientProviderFactory;
        private readonly ILogger<HttpRequestHandler> _logger;
        HttpServer _httpServer;
        public HttpRequestHandler(RequestTimeLimter requestTimeLimter,
            StaticFilesRequestHandler staticFilesRequestHandler,
            NetClientProviderFactory netClientProviderFactory,
            ILogger<HttpRequestHandler> logger)
        {
            _requestTimeLimter = requestTimeLimter;
            this._staticFilesRequestHandler = staticFilesRequestHandler;
            _netClientProviderFactory = netClientProviderFactory;
            _logger = logger;
        }
        public void SetServer(HttpServer httpServer)
        {
            _httpServer = httpServer;
        }
        public async Task WebSocketProxy(NetClient client, NetClient proxyClient)
        {
            client.ReadTimeout = 0;
            proxyClient.ReadTimeout = 0;

            _ = readSend(client, proxyClient);
            await readSend(proxyClient, client);
        }


        public async Task Handle(NetClient client)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var requestPathLine = await client.PipeReader.ReadHeaders(headers);

            headers.TryGetValue("X-Forwarded-For", out string x_for);

            var remote_ip = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
            var ip = RequestTimeLimter.GetRemoteIpAddress(HttpProxyProgram.Config.Current.ProxyIps , remote_ip, x_for);
            if (_requestTimeLimter.OnRequesting(ip) == false)
            {
                if (HttpProxyProgram.Config.Current.LogDetails)
                {
                    _logger.LogInformation($"{ip}访问次数太多，被拒绝访问.");
                }

                //输出401
                client.KeepAlive = false;
                client.OutputHttpCode(401, "Forbidden");
                return;
            }

            if (headers.TryGetValue("Host", out string host) == false)
                return;

            var config = _httpServer.Config.Proxies.FirstOrDefault(m => string.Equals(m.Host, host, StringComparison.OrdinalIgnoreCase));
            if (config == null)
                return;

            if (headers.TryGetValue("Connection", out string connection) && string.Equals(connection, "keep-alive", StringComparison.OrdinalIgnoreCase))
            {
                client.KeepAlive = true;
            }
            else if (headers.ContainsKey("Connection") == false)
            {
                client.KeepAlive = true;
            }

            if(!string.IsNullOrEmpty(config.RootPath))
            {
                await _staticFilesRequestHandler.Handle(client, headers, requestPathLine, config);
                return;
            }

            int inputContentLength = 0;
            if (headers.ContainsKey("Content-Length"))
            {
                int.TryParse(headers["Content-Length"], out inputContentLength);
            }


            if (headers.TryGetValue("X-Forwarded-For", out string xff))
            {
                headers["X-Forwarded-For"] = $"{xff}, {remote_ip}";
            }
            else
            {
                headers["X-Forwarded-For"] = remote_ip;
            }

            


            StringBuilder buffer = new StringBuilder();
            buffer.Append(requestPathLine);
            buffer.Append("\r\n");

            Uri target_uri = null;
            if(config.ChangeHostHeader && config.Target.StartsWith("http://") || config.Target.StartsWith("https://"))
            {
                target_uri = new Uri(config.Target);
            }
            foreach (var pair in headers)
            {
                //如果ChangeHostHeader为true，则修改Host头部和Origin头部
                if (config.ChangeHostHeader && target_uri != null && pair.Key == "Host")
                {
                    buffer.Append($"{pair.Key}: {target_uri.Authority}\r\n");
                }
                else if (config.ChangeHostHeader && target_uri != null && pair.Key == "Origin")
                {
                    var origin_uri = new Uri(pair.Value);
                    if (origin_uri.Authority == config.Host)
                    {
                        buffer.Append($"{pair.Key}: {origin_uri.Scheme}://{target_uri.Authority}{origin_uri.PathAndQuery}\r\n");
                    }
                    else
                    {
                        buffer.Append($"{pair.Key}: {pair.Value}\r\n");
                    }
                }
                else
                {
                    buffer.Append($"{pair.Key}: {pair.Value}\r\n");
                }
            }
            buffer.Append("\r\n");

            if (HttpProxyProgram.Config.Current.LogDetails)
            {
                _logger.LogInformation($"发送至目的地：\r\n{buffer}");
            }

            var data = Encoding.UTF8.GetBytes(buffer.ToString());

            var netClientProvider = _netClientProviderFactory.GetNetClientProvider(config.Target);
            var proxyClient = await netClientProvider.GetClientAsync(config.Target);

            try
            {
                proxyClient.InnerStream.Write(data, 0, data.Length);

                if (string.Equals(connection, "Upgrade", StringComparison.OrdinalIgnoreCase)
                   && headers.TryGetValue("Upgrade", out string upgrade)
                   && string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
                {
                    await WebSocketProxy(client, proxyClient);
                    return;
                }

                if (inputContentLength > 0)
                {
                    //发送upload数据到服务器
                    await client.ReadAndSend(proxyClient, inputContentLength);

                }
                else if (headers.TryGetValue("Transfer-Encoding", out string transferEncoding) && transferEncoding == "chunked")
                {
                    while (true)
                    {
                        var line = await client.ReadLineAsync(512);
                        proxyClient.WriteLine(line);
                        inputContentLength = Convert.ToInt32(line, 16);
                        if (inputContentLength == 0)
                        {
                            line = await client.ReadLineAsync(512);
                            proxyClient.WriteLine(line);
                            break;
                        }
                        else
                        {
                            await client.ReadAndSend(proxyClient, inputContentLength);

                            line = await client.ReadLineAsync(512);
                            proxyClient.WriteLine(line);
                        }
                    }
                }

                //读取服务器发回来的头部
                headers.Clear();
                requestPathLine = await proxyClient.PipeReader.ReadHeaders(headers);


                if (HttpProxyProgram.Config.Current.LogDetails)
                {
                    _logger.LogInformation($"接收回来的头部：\r\n{requestPathLine}\r\n{headers.ToJsonString(true)}");
                }

                inputContentLength = 0;
                if (headers.ContainsKey("Content-Length"))
                {
                    int.TryParse(headers["Content-Length"], out inputContentLength);
                }

                buffer.Clear();
                buffer.Append(requestPathLine);
                buffer.Append("\r\n");

                foreach (var pair in headers)
                {
                    buffer.Append($"{pair.Key}: {pair.Value}\r\n");
                }

                buffer.Append("\r\n");

                data = Encoding.UTF8.GetBytes(buffer.ToString());
                //发送头部给浏览器
                client.Write(data);

                if (inputContentLength > 0)
                {
                    await proxyClient.ReadAndSend(client, inputContentLength);
                }
                else if(headers.TryGetValue("Content-Type",out string resContentType) && resContentType == "text/event-stream")
                {
                    client.KeepAlive = false;
                    await proxyClient.PipeReader.ReadAndSend(client);
                }
                else if (headers.TryGetValue("Transfer-Encoding", out string transferEncoding) && transferEncoding == "chunked")
                {
                    while (true)
                    {
                        var line = await proxyClient.ReadLineAsync(512);
                        client.WriteLine(line);
                        inputContentLength = Convert.ToInt32(line, 16);
                        if (inputContentLength == 0)
                        {
                            line = await proxyClient.ReadLineAsync(512);
                            client.WriteLine(line);
                            break;
                        }
                        else
                        {
                            await proxyClient.ReadAndSend(client, inputContentLength);

                            line = await proxyClient.ReadLineAsync(512);
                            client.WriteLine(line);
                        }
                    }
                }

                if (client.KeepAlive)
                {
                    proxyClient.KeepAlive = true;
                    netClientProvider.AddClientToPool(config.Target, proxyClient);
                }
                else
                {
                    proxyClient.Dispose();
                }
            }
            catch
            {
                proxyClient.Dispose();
            }
        }

        async Task readSend(NetClient sender, NetClient reader)
        {
            int len = 10240;
            var buffer = ArrayPool<byte>.Shared.Rent(len);

            try
            {
                while (true)
                {
                    var readed = await reader.InnerStream.ReadAsync(buffer, 0, len);
                    if (readed <= 0)
                    {
                        reader.Dispose();
                        sender.Dispose();
                        return;
                    }
                    await sender.InnerStream.WriteAsync(buffer, 0, readed);
                }
            }
            catch
            {
                reader.Dispose();
                sender.Dispose();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
