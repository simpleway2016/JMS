using JMS.Dtos;
using JMS.ServerCore;
using JMS.ServerCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Applications.HttpMiddlewares
{
    internal class ProxyMiddleware : IHttpMiddleware
    {
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IDictionary<string, string> reqheaders)
        {
            var serviceName = requestPath.Substring(1);
            if (serviceName.Contains("/"))
            {
                serviceName = serviceName.Substring(0, serviceName.IndexOf("/"));
            }

            int contentLength = 0;
            if (reqheaders.ContainsKey("Content-Length"))
            {
                int.TryParse(reqheaders["Content-Length"], out contentLength);
            }

            using var rc = new RemoteClient(WebApiProgram.GatewayAddresses);
            var service = await rc.TryGetMicroServiceAsync(serviceName);

            if (service == null || service.ServiceLocation.AllowGatewayProxy == false)
            {
                if (contentLength > 0)
                {
                    await HttpHelper.ReadAndSend(client, null, contentLength);
                }
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
            else if (service.ServiceLocation.Type == ServiceType.JmsService)
            {
                await ProxyJmsService(rc, service, serviceName, client, requestPath, contentLength, reqheaders);
                return true;
            }

            Uri hostUri = null;
            if (service.ServiceLocation.ServiceAddress.Contains("://"))
            {
                hostUri = new Uri(service.ServiceLocation.ServiceAddress.ToLower());
            }
            else if (service.ServiceLocation.UseSsl)
            {
                hostUri = new Uri($"https://{service.ServiceLocation.ServiceAddress}:{service.ServiceLocation.Port}");
            }
            else
            {
                hostUri = new Uri($"http://{service.ServiceLocation.ServiceAddress}:{service.ServiceLocation.Port}");
            }

            NetClient proxyClient = await NetClientPool.CreateClientAsync(null, new NetAddress(hostUri.Host, hostUri.Port, hostUri.Scheme == "https" || hostUri.Scheme == "wss")
            {
                CertDomain = hostUri.Host
            });
            try
            {
                StringBuilder strBuffer = new StringBuilder();

                Uri gatewayUri = new Uri($"http://{reqheaders["Host"]}");

             
                strBuffer.AppendLine($"{httpMethod} {requestPath} HTTP/1.1");

                foreach (var pair in reqheaders)
                {
                    if (pair.Key == "TranId")
                        continue;
                    else if (pair.Key == "Tran")
                        continue;
                    else if (pair.Key == "TranFlag")
                        continue;
                    else if (pair.Key == "Host")
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
                if (contentLength > 0)
                {
                    //发送upload数据到服务器
                    await HttpHelper.ReadAndSend(client, proxyClient, contentLength);
                }
                else if (reqheaders.TryGetValue("Transfer-Encoding", out string transferEncoding) && transferEncoding == "chunked")
                {
                    while (true)
                    {
                        var line = await client.ReadLineAsync(512);
                        proxyClient.WriteLine(line);
                        contentLength = Convert.ToInt32(line, 16);
                        if (contentLength == 0)
                        {
                            line = await client.ReadLineAsync(512);
                            proxyClient.WriteLine(line);
                            break;
                        }
                        else
                        {
                            await HttpHelper.ReadAndSend(client, proxyClient, contentLength);

                            line = await client.ReadLineAsync(512);
                            proxyClient.WriteLine(line);
                        }
                    }
                }

                //读取服务器发回来的头部
                var headers = new Dictionary<string, string>();
                var requestPathLine = await JMS.ServerCore.HttpHelper.ReadHeaders(proxyClient.PipeReader, headers);
                contentLength = 0;
                if (headers.ContainsKey("Content-Length"))
                {
                    int.TryParse(headers["Content-Length"], out contentLength);
                }

                strBuffer.Clear();
                strBuffer.AppendLine(requestPathLine);

                foreach (var pair in headers)
                {
                    strBuffer.AppendLine($"{pair.Key}: {pair.Value}");
                }

                strBuffer.AppendLine("");
                data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部给浏览器
                client.Write(data);

                if (contentLength > 0)
                {
                    await HttpHelper.ReadAndSend(proxyClient, client, contentLength);
                }
                else if (headers.TryGetValue("Transfer-Encoding", out string transferEncoding) && transferEncoding == "chunked")
                {
                    while (true)
                    {
                        var line = await proxyClient.ReadLineAsync(512);
                        client.WriteLine(line);
                        contentLength = Convert.ToInt32(line, 16);
                        if (contentLength == 0)
                        {
                            line = await proxyClient.ReadLineAsync(512);
                            client.WriteLine(line);
                            break;
                        }
                        else
                        {
                            await HttpHelper.ReadAndSend(proxyClient, client, contentLength);

                            line = await proxyClient.ReadLineAsync(512);
                            client.WriteLine(line);
                        }
                    }
                }

                if (client.KeepAlive)
                {
                    NetClientPool.AddClientToPool(proxyClient);
                }
                else
                {
                    proxyClient.Dispose();
                }
            }
            catch (Exception)
            {
                proxyClient.Dispose();
                throw;
            }

            return true;
        }

        static async Task ProxyJmsService(RemoteClient rc, IMicroService service, string serviceName, NetClient client, string requestPath, int inputContentLength, IDictionary<string,string> headers)
        {
            //获取方法名
            try
            {
                var method = requestPath.Substring(serviceName.Length + 2);
                object[] _parames = null;
                if (inputContentLength > 0)
                {
                    var data = new byte[inputContentLength];
                    await client.ReadDataAsync(data, 0, inputContentLength);
                    var json = Encoding.UTF8.GetString(data);
                    _parames = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(json);
                }

                foreach (var header in headers)
                {
                    if (header.Key == "TranId")
                        continue;
                    else if (header.Key == "Tran")
                        continue;
                    else if (header.Key == "TranFlag")
                        continue;

                    rc.SetHeader(header.Key, header.Value.ToString());
                }

                object ret = null;
                if (_parames == null)
                {
                    ret = await service.InvokeAsync<object>(method);
                }
                else
                    ret = await service.InvokeAsync<object>(method, _parames);

                if (ret == null)
                {
                    client.OutputHttp200(null);
                }
                else if (ret is string)
                {
                    client.OutputHttp200((string)ret);
                }
                else
                {
                    if (ret.GetType().IsValueType)
                    {
                        client.OutputHttp200(ret.ToString());
                    }
                    else
                    {
                        client.OutputHttp200(ret.ToJsonString());
                    }
                }
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;

                if (ex.Message.StartsWith("Authentication failed"))
                {
                    client.OutputHttp401();
                }
                else
                {
                    client.OutputHttp500(ex.Message);
                }
            }
        }


    }
}
