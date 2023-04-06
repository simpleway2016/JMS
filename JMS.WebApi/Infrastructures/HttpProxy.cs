
using JMS.Dtos;
using JMS.WebApiDocument;
using JMS.WebApiDocument.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Tls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Way.Lib;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;
using static Org.BouncyCastle.Math.EC.ECCurve;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace JMS.Infrastructures
{
    internal class HttpProxy
    {

        public static async Task WebSocketProxy(NetClient client,string requestPathLine, string requestPath, GatewayCommand cmd)
        {
            if (requestPath.Contains("../"))
            {
                client.KeepAlive = false;
                client.OutputHttpNotFund();
                return;
            }

            var ip = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
            if (cmd.Header.TryGetValue("X-Forwarded-For", out string xff))
            {
                if (xff.Contains(ip) == false)
                    xff += $", {ip}";
            }
            else
            {
                cmd.Header["X-Forwarded-For"] = ip;
            }
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
            var service = rc.TryGetMicroService(servieName);
            if (service == null || service.ServiceLocation.AllowGatewayProxy == false)
            {
                return;
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

            Uri gatewayUri = new Uri($"http://{cmd.Header["Host"]}");

            var requestLineArgs = requestPathLine.Split(' ').Where(m => string.IsNullOrWhiteSpace(m) == false).ToArray();
            requestLineArgs[1] = requestPath;
            requestPathLine = string.Join(' ', requestLineArgs);
            strBuffer.AppendLine(requestPathLine);

            foreach (var pair in cmd.Header)
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

            readAndSend(proxyClient, client);

            await readAndSend(client, proxyClient);
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

        static async Task ProxyJmsService(RemoteClient rc, IMicroService service,string serviceName, NetClient client, string requestPath, int inputContentLength, GatewayCommand cmd)
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

                foreach (var header in cmd.Header)
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

                if (ex.Message == "Authentication failed")
                {
                    client.OutputHttp401();
                }
                else
                {
                    client.OutputHttp500(ex.Message);
                }
            }
        }

        static async Task ProxyJmsDoc( NetClient client,string requestPath,GatewayCommand cmd)
        {
            if(WebApiProgram.Configuration.GetSection("Http:SupportJmsDoc").Get<bool>() == false)
            {
                client.OutputHttpNotFund();
                return;
            }

            if(requestPath.StartsWith("/JmsDoc/vue.js" , StringComparison.OrdinalIgnoreCase))
            {
                if (cmd.Header.ContainsKey("If-Modified-Since"))
                {
                    client.OutputHttpCode(304, "NotModified");
                    return;
                }

                using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.vue.js"))
                {
                    var bs = new byte[ms.Length];
                    ms.Read(bs, 0, bs.Length);
                    var text = Encoding.UTF8.GetString(bs);

                    client.OutputHttp200(text, "text/javascript", "Last-Modified: Fri , 12 May 2006 18:53:33 GMT");
                }
                return;
            }

            List<ControllerInfo> controllerInfos = new List<ControllerInfo>();
            List<string> doneList = new List<string>();
            using (var rc = new RemoteClient(WebApiProgram.GatewayAddresses))
            {
                var allServices = await rc.ListMicroServiceAsync(null);
                foreach (var serviceRunningInfo in allServices)
                {
                    foreach (var serviceInfo in serviceRunningInfo.ServiceList)
                    {
                        if (serviceInfo.AllowGatewayProxy == false || doneList.Contains(serviceInfo.Name))
                            continue;

                        try
                        {
                            doneList.Add(serviceInfo.Name);

                            var service = await rc.TryGetMicroServiceAsync(serviceInfo.Name);
                            if (service == null)
                                continue;

                            if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.JmsService)
                            {
                                var jsonContent = service.GetServiceInfo();
                                var controllerInfo = jsonContent.FromJson<ControllerInfo>();
                                if (!string.IsNullOrWhiteSpace(serviceInfo.Description))
                                {
                                    controllerInfo.desc = serviceInfo.Description;
                                }
                                foreach (var method in controllerInfo.items)
                                {
                                    method.url = $"/{HttpUtility.UrlEncode(serviceInfo.Name)}/{method.title}";
                                }
                                if (controllerInfo.items.Count == 1)
                                    controllerInfo.items[0].opened = true;

                                controllerInfo.buttons = null;
                                controllerInfos.Add(controllerInfo);
                            }
                            else if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.WebSocket)
                            {
                                var jsonContent = service.GetServiceInfo();
                                var cInfo = jsonContent.FromJson<ControllerInfo>();

                                var controllerInfo = new ControllerInfo()
                                {
                                    name = serviceInfo.Name,
                                    desc = string.IsNullOrWhiteSpace(serviceInfo.Description) ? serviceInfo.Name : serviceInfo.Description,
                                };
                                controllerInfo.items = new List<MethodItemInfo>();
                                controllerInfo.items.Add(new MethodItemInfo
                                {
                                    title = "WebSocket接口",
                                    method = cInfo.desc,
                                    isComment = true,
                                    isWebSocket = true,
                                    opened = true,
                                    url = $"/{HttpUtility.UrlEncode(serviceInfo.Name)}"
                                });
                                controllerInfos.Add(controllerInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            
                        }
                    }
                }
            }

            using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.index.html"))
            {
                var bs = new byte[ms.Length];
                ms.Read(bs, 0, bs.Length);
                var text = Encoding.UTF8.GetString(bs).Replace("$$Controllers$$", controllerInfos.OrderBy(m => m.desc).ToJsonString()).Replace("$$Types$$", "[]");
                client.OutputHttp200(text);
            }
        }

        public static async Task Proxy(NetClient client,string requestPathLine, string requestPath, int inputContentLength, GatewayCommand cmd)
        {
            if (requestPath.Contains(".."))
            {
                client.KeepAlive = false;
                client.OutputHttpNotFund();
                return;
            }

            if (requestPath.StartsWith("/JmsDoc", StringComparison.OrdinalIgnoreCase))
            {
                await ProxyJmsDoc(client, requestPath,cmd);
                return;
            }


            var ip = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
            if (cmd.Header.TryGetValue("X-Forwarded-For", out string xff))
            {
                if (xff.Contains(ip) == false)
                    xff += $", {ip}";
            }
            else
            {
                cmd.Header["X-Forwarded-For"] = ip;
            }
           
            var serviceName = requestPath.Substring(1);
            if (serviceName.Contains("/"))
            {
                serviceName = serviceName.Substring(0, serviceName.IndexOf("/"));
            }

            using var rc = new RemoteClient(WebApiProgram.GatewayAddresses);
            var service = rc.TryGetMicroService(serviceName);

            if (service == null || service.ServiceLocation.AllowGatewayProxy == false)
            {
                if (inputContentLength > 0)
                {
                    await client.ReadDataAsync(new byte[inputContentLength], 0, inputContentLength);
                }
                client.OutputHttpNotFund();
                return;
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
                await ProxyJmsService(rc , service,serviceName, client, requestPath, inputContentLength, cmd);
                return;
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

                Uri gatewayUri = new Uri($"http://{cmd.Header["Host"]}");

                var requestLineArgs = requestPathLine.Split(' ').Where(m => string.IsNullOrWhiteSpace(m) == false).ToArray();
                requestLineArgs[1] = requestPath;
                requestPathLine = string.Join(' ', requestLineArgs);
                strBuffer.AppendLine(requestPathLine);

                foreach (var pair in cmd.Header)
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
                if (inputContentLength > 0)
                {
                    //发送upload数据到服务器
                    data = new byte[inputContentLength];
                    await client.ReadDataAsync(data, 0, inputContentLength);
                    proxyClient.Write(data);
                }
                else if (cmd.Header.TryGetValue("Transfer-Encoding", out string transferEncoding) && transferEncoding == "chunked")
                {
                    while (true)
                    {
                        var line = await client.ReadLineAsync();
                        proxyClient.WriteLine(line);
                        inputContentLength = Convert.ToInt32(line, 16);
                        if (inputContentLength == 0)
                        {
                            line = await client.ReadLineAsync();
                            proxyClient.WriteLine(line);
                            break;
                        }
                        else
                        {
                            data = new byte[inputContentLength];
                            await client.ReadDataAsync(data, 0, inputContentLength);
                            proxyClient.InnerStream.Write(data, 0, inputContentLength);

                            line = await client.ReadLineAsync();
                            proxyClient.WriteLine(line);
                        }
                    }
                }

                //读取服务器发回来的头部
                var headers = new Dictionary<string, string>();
                requestPathLine = await ReadHeaders(proxyClient, headers);
                inputContentLength = 0;
                if (headers.ContainsKey("Content-Length"))
                {
                    int.TryParse(headers["Content-Length"], out inputContentLength);
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

                if (inputContentLength > 0)
                {
                    data = new byte[inputContentLength];
                    await proxyClient.ReadDataAsync(data, 0, inputContentLength);
                    client.Write(data);
                }
                else if (headers.TryGetValue("Transfer-Encoding", out string transferEncoding) && transferEncoding == "chunked")
                {
                    while (true)
                    {
                        var line = await proxyClient.ReadLineAsync();
                        client.WriteLine(line);
                        inputContentLength = Convert.ToInt32(line, 16);
                        if (inputContentLength == 0)
                        {
                            line = await proxyClient.ReadLineAsync();
                            client.WriteLine(line);
                            break;
                        }
                        else
                        {
                            data = new byte[inputContentLength];
                            await proxyClient.ReadDataAsync(data, 0, inputContentLength);
                            client.InnerStream.Write(data, 0, inputContentLength);

                            line = await proxyClient.ReadLineAsync();
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
        }

        /// <summary>
        /// 重定向
        /// </summary>
        /// <param name="client"></param>
        /// <param name="requestPath"></param>
        public static void Redirect(NetClient client, IServiceProviderAllocator serviceProviderAllocator, string requestPath)
        {
            try
            {
                var servieName = requestPath.Substring(1);
                servieName = servieName.Substring(0, servieName.IndexOf("/"));

                requestPath = requestPath.Substring(servieName.Length + 1);
                //重定向
                var location = serviceProviderAllocator.Alloc(new GetServiceProviderRequest
                {
                    ServiceName = servieName
                });
                if (location != null && !string.IsNullOrEmpty(location.ServiceAddress))
                {
                    var serverAddr = location.ServiceAddress;
                    if (serverAddr.EndsWith("/"))
                        serverAddr = serverAddr.Substring(0, serverAddr.Length);

                    client.OutputHttpRedirect($"{serverAddr}{requestPath}");
                }
                else
                {
                    client.OutputHttpNotFund();
                }
            }
            catch
            {
                client.OutputHttpNotFund();
            }
        }

        public static async Task<string> ReadHeaders( NetClient client, IDictionary<string, string> headers)
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
                        requestPathLine = line;

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
    }
}
