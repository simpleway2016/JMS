using JMS.Dtos;
using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

            using (NetClient proxyClient = new NetClient())
            {
                await proxyClient.ConnectAsync(new NetAddress(hostUri.Host, hostUri.Port));
                if (hostUri.Scheme == "https" || hostUri.Scheme == "wss")
                {
                    await proxyClient.AsSSLClientAsync(hostUri.Host, null, System.Security.Authentication.SslProtocols.None, (sender, certificate, chain, sslPolicyErrors) => true);
                }

                StringBuilder strBuffer = new StringBuilder();
                var path = context.Request.GetEncodedPathAndQuery();
                var index = path.IndexOf("/JMSRedirect/");
                path = path.Substring(index + 12);
                strBuffer.AppendLine($"GET {path} HTTP/1.1");

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
                    if (pair.Key == "Host")
                    {
                        strBuffer.AppendLine($"Host: {hostUri.Host}");
                    }
                    else
                    {
                        strBuffer.AppendLine($"{pair.Key}: {pair.Value.ToString()}");
                    }
                }

                strBuffer.AppendLine($"X-Forwarded-For: {strForwared}");
                strBuffer.AppendLine("");
                var data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部到服务器
                proxyClient.Write(data);

                NetClient client = new NetClient(new ConnectionStream(context));

                readAndSend(proxyClient, client);

                await readAndSend(client, proxyClient);
            }
        }

        /// <summary>
        /// 反向代理到webapi类型的微服务
        /// </summary>
        /// <param name="location"></param>
        /// <param name="context"></param>
        /// <param name="method"></param>
        /// <param name="redirectHeaders"></param>
        /// <returns></returns>
        internal static async Task ProxyWebAapi(ClientServiceDetail location, HttpContext context, string method, string[] redirectHeaders)
        {
            StringBuilder strBuffer = new StringBuilder();
            var path = context.Request.GetEncodedPathAndQuery();
            var index = path.IndexOf("/JMSRedirect/");
            path = path.Substring(index + 12 + location.Name.Length + 1);
            strBuffer.AppendLine($"{context.Request.Method} {path} HTTP/1.1");

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
            Uri hostUri = null;
            if (location.ServiceAddress.Contains("://"))
            {
                hostUri = new Uri(location.ServiceAddress.ToLower());
            }
            foreach (var pair in context.Request.Headers)
            {
                if (pair.Key == "Host" && hostUri != null)
                {
                    strBuffer.AppendLine($"Host: {hostUri.Host}");
                }
                else
                {
                    strBuffer.AppendLine($"{pair.Key}: {pair.Value.ToString()}");
                }
            }

            strBuffer.AppendLine($"X-Forwarded-For: {strForwared}");
            strBuffer.AppendLine("");

            NetClient proxyClient = await NetClientPool.CreateClientAsync(null, new NetAddress(hostUri.Host, hostUri.Port, hostUri.Scheme == "https" || hostUri.Scheme == "wss")
            {
                CertDomain = hostUri.Host
            });

            try
            {
                var data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部到服务器
                proxyClient.Write(data);

                var inputContentLength = (int)context.Request.ContentLength.GetValueOrDefault();

                if (inputContentLength > 0)
                {
                    data = new byte[inputContentLength];
                    await context.Request.Body.ReadAsync(data, 0, data.Length);
                    proxyClient.Write(data);
                }


                //读取服务器发回来的头部
                var headers = new Dictionary<string, string>();
                var requestPathLine = await ReadHeaders(null, proxyClient, headers);
                inputContentLength = 0;
                if (headers.ContainsKey("Content-Length"))
                {
                    int.TryParse(headers["Content-Length"], out inputContentLength);
                }

                strBuffer.Clear();

                foreach (var pair in headers)
                {
                    if (pair.Key == "TranId" || pair.Key == "Tran" || pair.Key == "TranFlag")
                        continue;
                    else if (pair.Key == "Host" || pair.Key == "Transfer-Encoding" || pair.Key == "Content-Length")
                    {
                        continue;
                    }
                    else
                    {
                        context.Response.Headers[pair.Key] = pair.Value;
                    }
                }

               
                if (inputContentLength > 0)
                {
                    data = new byte[inputContentLength];
                    await proxyClient.ReadDataAsync(data, 0, inputContentLength);
                    await context.Response.WriteAsync(Encoding.UTF8.GetString(data));
                }
                else if (headers.TryGetValue("Transfer-Encoding", out string transferEncoding) && transferEncoding == "chunked")
                {
                    while (true)
                    {
                        var line = await proxyClient.ReadLineAsync();
                        inputContentLength = Convert.ToInt32(line, 16);
                        if (inputContentLength == 0)
                        {
                            line = await proxyClient.ReadLineAsync();
                            break;
                        }
                        else
                        {
                            data = new byte[inputContentLength];
                            await proxyClient.ReadDataAsync(data, 0, inputContentLength);
                            await context.Response.WriteAsync(Encoding.UTF8.GetString(data));

                            line = await proxyClient.ReadLineAsync();
                        }
                    }
                }

                if (headers.TryGetValue("Connection", out string connection) && string.Equals(connection, "keep-alive", StringComparison.OrdinalIgnoreCase))
                {
                    NetClientPool.AddClientToPool(proxyClient);
                }
                else if (headers.ContainsKey("Connection") == false)
                {
                    NetClientPool.AddClientToPool(proxyClient);
                }               

            }
            catch (Exception)
            {
                proxyClient.Dispose();
                throw;
            }
           

        }

        public static async Task<string> ReadHeaders(string preRequestString, NetClient client, IDictionary<string, string> headers)
        {
            List<byte> lineBuffer = new List<byte>(1024);
            string line = null;
            string requestPathLine = null;
            byte[] bData = new byte[1];
            int readed;
            int indexFlag;
            while (true)
            {
                readed = await client.InnerStream.ReadAsync(bData, 0, 1);
                if (readed <= 0)
                    throw new SocketException();

                if (bData[0] == 10)
                {
                    line = Encoding.UTF8.GetString(lineBuffer.ToArray());
                    lineBuffer.Clear();
                    if (requestPathLine == null)
                        requestPathLine = preRequestString + line;

                    if (line == "")
                    {
                        break;
                    }
                    else if ((indexFlag = line.IndexOf(":")) > 0 && indexFlag < line.Length - 1)
                    {
                        var key = line.Substring(0, indexFlag);
                        var value = line.Substring(indexFlag + 1).Trim();
                        if (headers.ContainsKey(key) == false)
                        {
                            headers[key] = value;
                        }
                    }
                }
                else if (bData[0] != 13)
                {
                    lineBuffer.Add(bData[0]);
                }
            }
            return requestPathLine;
        }

        static async Task readAndSend(NetClient readClient, NetClient writeClient)
        {
            try
            {
                byte[] recData = new byte[4096];
                int readed;
                while (true)
                {
                    readed = await readClient.InnerStream.ReadAsync(recData, 0, recData.Length);
                    if (readed <= 0)
                        break;
                    writeClient.InnerStream.Write(recData, 0, readed);
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                readClient.Dispose();
                writeClient.Dispose();
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
            using (var client = ClientProviderFunc())
            {
                var service = await client.GetMicroServiceAsync(config.ServiceName);
                if(ServiceRedirects.Configs == null && service.ServiceLocation.AllowGatewayProxy == false)
                {
                    //不允许反向代理
                    config.Handled = true;
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("not found");
                    return null;
                }
                if( service.ServiceLocation.Type == JMS.Dtos.ServiceType.WebSocket)
                {
                    config.Handled = true;
                    client.Dispose();
                    await InvokeWebSocket(service.ServiceLocation, context, method, redirectHeaders);                    
                    return null;
                }
                else if(service.ServiceLocation.Type == ServiceType.WebApi)
                {
                    config.Handled = true;
                    client.Dispose();
                    await ProxyWebAapi(service.ServiceLocation, context, method, redirectHeaders);
                    return null;
                }


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
