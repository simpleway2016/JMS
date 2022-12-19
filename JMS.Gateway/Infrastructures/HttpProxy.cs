using JMS.Dtos;
using Org.BouncyCastle.Crypto.Tls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Infrastructures
{
    internal class HttpProxy
    {
        
        public static async Task WebSocketProxy(NetClient client, IServiceProviderAllocator serviceProviderAllocator, string requestPathLine, string requestPath, GatewayCommand cmd)
        {
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
            if(servieName.Contains("?"))
            {
                servieName = servieName.Substring(0, servieName.IndexOf("?"));
            }

            var location = serviceProviderAllocator.Alloc(new GetServiceProviderRequest
            {
                ServiceName = servieName,
                IsGatewayProxy = true,
                Header = cmd.Header
            });
            if (location == null)
            {
                return;
            }

            if (location.Type == ServiceType.WebApi)
            {
                //去除servicename去代理访问
                requestPath = requestPath.Substring(servieName.Length + 1);
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
            NetClient proxyClient = client.PairClient;
            if (proxyClient == null)
            {
                proxyClient = new NetClient();
                await proxyClient.ConnectAsync(hostUri.Host, hostUri.Port);
                if (hostUri.Scheme == "https" || hostUri.Scheme == "wss")
                {
                   await proxyClient.AsSSLClientAsync(hostUri.Host, null, System.Security.Authentication.SslProtocols.None, (sender, certificate, chain, sslPolicyErrors) => true);
                }
                client.PairClient = proxyClient;
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
           
            readAndSend(proxyClient.InnerStream, client.InnerStream);

            await readAndSend(client.InnerStream, proxyClient.InnerStream);
        }

        static async Task readAndSend(Stream readStream ,Stream writeStream)
        {
            byte[] recData = new byte[4096];
            int readed;
            while (true)
            {
                readed = await readStream.ReadAsync(recData, 0, recData.Length);
                if(readed < 0)
                    break;
                writeStream.Write(recData, 0, readed);
            }
        }


        public static async Task Proxy(NetClient client,IServiceProviderAllocator serviceProviderAllocator, string requestPathLine, string requestPath, int inputContentLength, GatewayCommand cmd)
        {
            var ip = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
            if (cmd.Header.TryGetValue("X-Forwarded-For",out string xff))
            {              
                if (xff.Contains(ip) == false)
                    xff += $", {ip}";
            }
            else
            {
                cmd.Header["X-Forwarded-For"] = ip;
            }
            var servieName = requestPath.Substring(1);
            if(servieName.Contains("/") )
            {
                servieName = servieName.Substring(0, servieName.IndexOf("/"));
            }
           

            var location = serviceProviderAllocator.Alloc(new GetServiceProviderRequest
            {
                ServiceName = servieName,
                IsGatewayProxy = true,
                Header = cmd.Header
            });
            if(location == null)
            {
                if (inputContentLength > 0)
                {
                    await client.ReadDataAsync(new byte[inputContentLength], 0, inputContentLength);
                }
                client.OutputHttpNotFund();
                return;
            }

            if (location.Type == ServiceType.WebApi)
            {
                //去除servicename去代理访问
                requestPath = requestPath.Substring(servieName.Length + 1);
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

            NetClient proxyClient = client.PairClient;
            if (proxyClient == null)
            {
                proxyClient = new NetClient();
                await proxyClient.ConnectAsync(hostUri.Host, hostUri.Port);
                if (hostUri.Scheme == "https" || hostUri.Scheme == "wss")
                {
                    await proxyClient.AsSSLClientAsync(hostUri.Host, null, System.Security.Authentication.SslProtocols.None, (sender, certificate, chain, sslPolicyErrors) => true);
                }
                client.PairClient = proxyClient;
            }

            StringBuilder strBuffer = new StringBuilder();
           
            Uri gatewayUri = new Uri($"http://{cmd.Header["Host"]}");

            var requestLineArgs = requestPathLine.Split(' ').Where(m=>string.IsNullOrWhiteSpace(m) == false).ToArray();
            requestLineArgs[1] = requestPath;
            requestPathLine = string.Join(' ', requestLineArgs);
            strBuffer.AppendLine(requestPathLine);

            foreach ( var pair in cmd.Header)
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
            if(inputContentLength > 0)
            {
                //发送upload数据到服务器
                data = new byte[inputContentLength];
                await client.ReadDataAsync(data, 0, inputContentLength);
                proxyClient.Write(data);
            }

            //读取服务器发回来的头部
            var headers = new Dictionary<string, string>();
            requestPathLine = ReadHeaders(null,proxyClient, headers);
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
            else if (headers.TryGetValue("Transfer-Encoding" , out string transferEncoding) && transferEncoding == "chunked")
            {
                while (true)
                {
                    var line = proxyClient.ReadLine();
                    client.WriteLine(line);
                    inputContentLength = Convert.ToInt32(line, 16);
                    if(inputContentLength == 0)
                    {
                        line = proxyClient.ReadLine();
                        client.WriteLine(line);
                        break;
                    }
                    else
                    {
                        data = new byte[inputContentLength];
                        await proxyClient.ReadDataAsync(data, 0, inputContentLength);
                        client.InnerStream.Write(data,0,inputContentLength);

                        line = proxyClient.ReadLine();
                        client.WriteLine(line);
                    }
                }
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

        public static string ReadHeaders(string preRequestString, NetClient client, IDictionary<string,string> headers)
        {
            List<byte> lineBuffer = new List<byte>(1024);
            string line = null;
            string requestPathLine = null;
          
            while (true)
            {
                int bData = client.InnerStream.ReadByte();
                if (bData == 13)
                {
                    line = Encoding.UTF8.GetString(lineBuffer.ToArray());
                    lineBuffer.Clear();
                    if (requestPathLine == null)
                        requestPathLine = preRequestString + line;

                    if (line == "")
                    {
                        bData = client.InnerStream.ReadByte();
                       

                        break;
                    }
                    else if (line.Contains(":"))
                    {
                        var arr = line.Split(':');
                        if (arr.Length >= 2)
                        {
                            var key = arr[0].Trim();
                            var value = arr[1].Trim();
                            if (headers.ContainsKey(key) == false)
                            {
                                headers[key] = value;
                            }
                        }
                    }
                }
                else if (bData != 10)
                {
                    lineBuffer.Add((byte)bData);
                }
            }
            return requestPathLine;
        }
    }
}
