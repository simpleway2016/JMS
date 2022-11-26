using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace JMS.Applications
{
    class HttpHandler : IRequestHandler
    {
        MicroServiceHost _MicroServiceProvider;
        ControllerFactory _controllerFactory;
        public InvokeType MatchType => InvokeType.Http;
        public HttpHandler(ControllerFactory controllerFactory, MicroServiceHost microServiceProvider)
        {
            this._MicroServiceProvider = microServiceProvider;
            this._controllerFactory = controllerFactory;

        }

        /// <summary>
        /// 响应串
        /// </summary>
        public string GetResponse(IDictionary<string,string> header)
        {
                string secWebSocketKey = header["Sec-WebSocket-Key"].ToString();
                string m_Magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                string responseKey = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.ASCII.GetBytes(secWebSocketKey + m_Magic)));

                StringBuilder response = new StringBuilder(); //响应串
                response.Append("HTTP/1.1 101 Web Socket Protocol JMS\r\n");

                //将请求串的键值转换为对应的响应串的键值并添加到响应串
                response.AppendFormat("Upgrade: {0}\r\n", header["Upgrade"]);
                response.AppendFormat("Connection: {0}\r\n", header["Connection"]);
                response.AppendFormat("Sec-WebSocket-Accept: {0}\r\n", responseKey);
                if (header.ContainsKey("Origin"))
                {
                    response.AppendFormat("WebSocket-Origin: {0}\r\n", header["Origin"]);
                }
                if (header.ContainsKey("Host"))
                {
                    response.AppendFormat("WebSocket-Location: {0}\r\n", header["Host"]);
                }

                response.Append("\r\n");

                return response.ToString();
            
        }

        public void Handle(NetClient netclient, InvokeCommand cmd)
        {
            cmd.Header = new Dictionary<string, string>();
            var urlLine = ReadHeaders(cmd.Service, netclient, cmd.Header);

            

            string subProtocol = null;
            cmd.Header.TryGetValue("Sec-WebSocket-Protocol", out subProtocol);//[Connection, Upgrade] //Upgrade, websocket

            var websocket = WebSocket.CreateFromStream(netclient.InnerStream, true, subProtocol, TimeSpan.FromSeconds(2));

            var path = urlLine.Split(' ')[1];
            if (path.StartsWith("/") == false)
                path = "/" + path;

            var route = path.Split('/');
            cmd.Service = route[1];
            if (cmd.Service.Contains("?"))
            {
                cmd.Service = cmd.Service.Substring(0, cmd.Service.IndexOf("?"));
            }
            var controllerTypeInfo = _controllerFactory.GetControllerType(cmd.Service);

            object userContent = null;
            if (controllerTypeInfo.NeedAuthorize)
            {
                var auth = _MicroServiceProvider.ServiceProvider.GetService<IAuthenticationHandler>();
                if (auth != null)
                {
                    try
                    {
                        userContent = auth.Authenticate(cmd.Header);
                    }
                    catch
                    {
                        var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 401 NotAllow\r\nAccess-Control-Allow-Origin: *\r\n\r\n");
                        netclient.Write(data);
                        Thread.Sleep(2000);
                        return;
                    }
                }
            }


            var responseText = GetResponse(cmd.Header);
            netclient.InnerStream.Write(Encoding.UTF8.GetBytes(responseText));

            try
            {
                using (IServiceScope serviceScope = _MicroServiceProvider.ServiceProvider.CreateScope())
                {
                    MicroServiceControllerBase.RequestingObject.Value =
                        new MicroServiceControllerBase.LocalObject(netclient.RemoteEndPoint, cmd, serviceScope.ServiceProvider, userContent, path);

                    var controller = (WebSocketController)_controllerFactory.CreateController(serviceScope, controllerTypeInfo);
                    controller.OnConnected(websocket).Wait();
                }
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                MicroServiceControllerBase.RequestingObject.Value = null;
            }
        }


        public static string ReadHeaders(string preRequestString, NetClient client, IDictionary<string, string> headers)
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
