using JMS.Dtos;
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
    internal class ProxyWebApi
    {
        /// <summary>
        /// 反向代理到webapi类型的微服务
        /// </summary>
        /// <param name="location"></param>
        /// <param name="context"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        internal static async Task Run(ClientServiceDetail location, HttpContext context, string method)
        {
            StringBuilder strBuffer = new StringBuilder();
            var path = context.Request.GetEncodedPathAndQuery();
            var index = path.IndexOf("/JMSRedirect/");
            path = path.Substring(index + 12 + location.Name.Length + 1);


            strBuffer.Append($"{context.Request.Method} {path} HTTP/1.1\r\n");

            var ip = context.Connection.RemoteIpAddress.ToString();
            string strForwared = null;

            Uri gatewayUri = new Uri($"http://{context.Request.Headers["Host"]}");

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
                if (pair.Key == "TranId")
                    continue;
                else if (pair.Key == "Tran")
                    continue;
                else if (pair.Key == "TranFlag")
                    continue;
                else
                {
                    strBuffer.Append($"{pair.Key}: {pair.Value}\r\n");
                }
            }

            strBuffer.Append($"X-Forwarded-For: {strForwared}\r\n");
            strBuffer.Append("\r\n");

            NetClient proxyClient = await NetClientPool.CreateClientAsync(null, new NetAddress(hostUri.Host, hostUri.Port, hostUri.Scheme == "https" || hostUri.Scheme == "wss")
            {
                CertDomain = hostUri.Host
            });

            using (var client = RequestHandler.ClientProviderFunc())
            {
                proxyClient.ReadTimeout = client.Timeout;
            }

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
                var requestPathLine = await proxyClient.PipeReader.ReadHeaders(headers);
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
    }
}
