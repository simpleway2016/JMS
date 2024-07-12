using JMS.Dtos;
using JMS.ServerCore;
using JMS.ServerCore.Http;
using Microsoft.Extensions.Configuration;
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
        private readonly IWebApiHostEnvironment _webApiEnvironment;
        int _timeout;
        public ProxyMiddleware(IWebApiHostEnvironment webApiEnvironment,IConfiguration configuration)
        {
            _webApiEnvironment = webApiEnvironment;
            var timeout = configuration.GetSection("InvokeTimeout").Get<int?>();
            if(timeout != null && timeout >= 0)
            {
                _timeout = timeout.Value;
            }
        }
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, Dictionary<string, string> reqheaders)
        {
            int indexflag;
            var serviceName = requestPath.Substring(1);
            if ((indexflag = serviceName.IndexOf('/')) > 0)
            {
                serviceName = serviceName.Substring(0, indexflag);
            }

            int contentLength = 0;
            if (reqheaders.ContainsKey("Content-Length"))
            {
                int.TryParse(reqheaders["Content-Length"], out contentLength);
            }

            using var rc = new RemoteClient(_webApiEnvironment.GatewayAddresses);
            rc.Timeout = _timeout;
            var service = await rc.TryGetMicroServiceAsync(serviceName);

            if (service == null || service.ServiceLocation.AllowGatewayProxy == false)
            {
                if (contentLength > 0)
                {
                    await client.ReadDataAsync( null,0, contentLength);
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
            proxyClient.ReadTimeout = rc.Timeout;

            try
            {
                StringBuilder strBuffer = new StringBuilder();

                Uri gatewayUri = new Uri($"http://{reqheaders["Host"]}");

             
                strBuffer.Append($"{httpMethod} {requestPath} HTTP/1.1\r\n");

                foreach (var pair in reqheaders)
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

                strBuffer.Append("\r\n");
                var data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部到服务器
                proxyClient.Write(data);
                if (contentLength > 0)
                {
                    //发送upload数据到服务器
                    await client.ReadAndSend( proxyClient, contentLength);
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
                            await client.ReadAndSend( proxyClient, contentLength);

                            line = await client.ReadLineAsync(512);
                            proxyClient.WriteLine(line);
                        }
                    }
                }

                //读取服务器发回来的头部
                var headers = new Dictionary<string, string>();
                var requestPathLine = await proxyClient.PipeReader.ReadHeaders( headers);
                contentLength = 0;
                if (headers.ContainsKey("Content-Length"))
                {
                    int.TryParse(headers["Content-Length"], out contentLength);
                }

                strBuffer.Clear();
                strBuffer.Append(requestPathLine);
                strBuffer.Append("\r\n");

                bool addedAllowOrigin = false;
                foreach (var pair in headers)
                {
                    if (!addedAllowOrigin && pair.Key == "Access-Control-Allow-Origin")
                    {
                        addedAllowOrigin = true;
                    }

                    strBuffer.Append($"{pair.Key}: {pair.Value}\r\n");
                }
                if (!addedAllowOrigin)
                {
                    strBuffer.Append($"Access-Control-Allow-Origin: *\r\n");
                }

                strBuffer.Append("\r\n");

                data = Encoding.UTF8.GetBytes(strBuffer.ToString());
                //发送头部给浏览器
                client.Write(data);

                if (contentLength > 0)
                {
                    await proxyClient.ReadAndSend(client, contentLength);
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
                            await proxyClient.ReadAndSend(client, contentLength);

                            line = await proxyClient.ReadLineAsync(512);
                            client.WriteLine(line);
                        }
                    }
                }

                if (client.KeepAlive)
                {
                    proxyClient.KeepAlive = true;
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

                InvokeResult<object> result = null;
                InvokeAttributes invokeAttributes = null;
                int statusCode = 200;
                if (_parames == null)
                {
                    result = await service.InvokeExAsync<object>(method);
                }
                else
                    result = await service.InvokeExAsync<object>(method, _parames);

                if(result.Attributes != null)
                {
                    invokeAttributes = result.Attributes.FromJson<InvokeAttributes>();
                    if (invokeAttributes.StatusCode != null)
                    {
                        statusCode = invokeAttributes.StatusCode.Value;
                    }
                }

                if (result.Data == null)
                {
                    client.OutputHttpCode(statusCode, Defines.GetStatusDescription(statusCode), null);
                }
                else if (result.Data is string)
                {
                    client.OutputHttpCode(statusCode, Defines.GetStatusDescription(statusCode), (string)result.Data);
                }
                else
                {
                    if (result.Data.GetType().IsValueType)
                    {
                        client.OutputHttpCode(statusCode, Defines.GetStatusDescription(statusCode), result.Data.ToString());
                    }
                    else
                    {
                        client.OutputHttpCode(statusCode, Defines.GetStatusDescription(statusCode), result.Data.ToJsonString());
                    }
                }
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;

                if (ex is RemoteException rex && rex.StatusCode != null)
                {
                    client.OutputHttpCode(rex.StatusCode.Value , "error" , ex.Message);
                }
                else
                {
                    client.OutputHttp500(ex.Message);
                }
            }
        }


    }
}
