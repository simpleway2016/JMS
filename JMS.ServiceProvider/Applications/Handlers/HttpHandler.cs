using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Sockets;
using Org.BouncyCastle.Ocsp;
using Microsoft.Extensions.Logging;
using System.Reflection;
using JMS.ServerCore;
using System.Security.Claims;
using System.Web;
using System.Collections.Specialized;
using JMS.Controllers;

namespace JMS.Applications
{
    class HttpHandler : IRequestHandler
    {
        MicroServiceHost _MicroServiceProvider;
        ControllerFactory _controllerFactory;
        ILogger<HttpHandler> _logger;
        IConnectionCounter _connectionCounter;
        public InvokeType MatchType => InvokeType.Http;

        static MethodInfo PingMethod;
        static object[] PingMethodParameters;
        public HttpHandler(ControllerFactory controllerFactory, MicroServiceHost microServiceProvider)
        {
            this._MicroServiceProvider = microServiceProvider;
            this._controllerFactory = controllerFactory;
            this._logger = microServiceProvider.ServiceProvider.GetService<ILogger<HttpHandler>>();
            _connectionCounter = microServiceProvider.ServiceProvider.GetService<IConnectionCounter>();
        }

        /// <summary>
        /// 获取websocket响应串
        /// </summary>
        public static string GetWebSocketResponse(IDictionary<string, string> header, ref string subProtocol)
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
            if (subProtocol != null)
            {
                if (subProtocol.Contains(","))
                {
                    subProtocol = subProtocol.Split(',')[0];
                }
                response.AppendFormat("Sec-WebSocket-Protocol: {0}\r\n", subProtocol);
            }

            response.Append("\r\n");

            return response.ToString();

        }

        public async Task Handle(NetClient netclient, InvokeCommand cmd)
        {
            cmd.Header = new Dictionary<string, string>();
            var urlLine = await netclient.PipeReader.ReadHeaders(cmd.Header);



            string subProtocol = null;
            cmd.Header.TryGetValue("Sec-WebSocket-Protocol", out subProtocol);//[Connection, Upgrade] //Upgrade, websocket



            var path = urlLine.Split(' ')[1];
            if (path.StartsWith("/") == false)
                path = "/" + path;

            var route = path.Split('/');
            cmd.Service = route[1];
            int qIndex;
            NameValueCollection requestQuery = null;
            if ((qIndex = cmd.Service.IndexOf("?")) > 0 && qIndex < cmd.Service.Length - 1)
            {
                requestQuery = HttpUtility.ParseQueryString(cmd.Service.Substring(qIndex + 1));
                cmd.Service = cmd.Service.Substring(0, qIndex);
               
            }
            var controllerTypeInfo = _controllerFactory.GetControllerType(cmd.Service);

            ClaimsPrincipal userContent = null;
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


            try
            {
                var responseText = GetWebSocketResponse(cmd.Header , ref subProtocol);
                netclient.InnerStream.Write(Encoding.UTF8.GetBytes(responseText));
            }
            catch (Exception)
            {
                return;
            }

            WebSocket websocket = null;
            try
            {
                netclient.ReadTimeout = 0;
                websocket = WebSocket.CreateFromStream(netclient.InnerStream, true, subProtocol, Timeout.InfiniteTimeSpan);
                _connectionCounter.WebSockets.TryAdd(websocket, true);

                 using (IServiceScope serviceScope = _MicroServiceProvider.ServiceProvider.CreateScope())
                {
                   var request = MicroServiceControllerBase.RequestingObject.Value =
                        new MicroServiceControllerBase.LocalObject(netclient.RemoteEndPoint, cmd, serviceScope.ServiceProvider, userContent, path);
                    request.RequestQuery = requestQuery??new NameValueCollection();

                    var controller = (WebSocketController)_controllerFactory.CreateController(serviceScope, controllerTypeInfo);
                    controller.SubProtocol = subProtocol;

                    await controller.OnConnected(websocket);
                }

                if (websocket.State == WebSocketState.Open || websocket.State == WebSocketState.CloseReceived)
                {
                    await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
            }
            catch (Exception)
            {
                if (websocket != null)
                {
                    if (websocket.State == WebSocketState.Open || websocket.State == WebSocketState.CloseReceived)
                    {
                        await websocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "", CancellationToken.None);
                    }
                }
            }
            finally
            {
                if (websocket != null)
                {
                    _connectionCounter.WebSockets.TryRemove(websocket, out bool o);
                    websocket.Dispose();
                }
                MicroServiceControllerBase.RequestingObject.Value = null;
            }

        }

    }
}
