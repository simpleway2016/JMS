﻿using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    public abstract class WebSocketController: BaseJmsController
    {      

        string _requestPath;
        /// <summary>
        /// 请求路径
        /// </summary>
        public string RequestPath
        {
            get
            {
                if (_requestPath == null && RequestingObject.Value != null)
                {
                    _requestPath = RequestingObject.Value.RequestPath;
                }
                return _requestPath;
            }
        }

        public abstract Task OnConnected(WebSocket webSocket);
    }

    public static class WebSocketExtens
    {
        public static async Task<string> ReadString(this WebSocket webSocket)
        {
            try
            {
                List<byte> list = null;
                byte[] data = new byte[4096];
                var buffer = new ArraySegment<byte>(data);
                int len;
                while (true)
                {
                    var ret = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    if (webSocket.State != WebSocketState.Open)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        return null;
                    }
                    len = ret.Count;
                    if (ret.EndOfMessage)
                    {
                        if (list != null)
                        {
                            list.AddRange(buffer.Slice(0, ret.Count));
                        }
                        break;
                    }
                    else
                    {
                        if (list == null)
                            list = new List<byte>();
                        list.AddRange(buffer.Slice(0, ret.Count));
                    }
                }
                if (list != null)
                    return Encoding.UTF8.GetString(list.ToArray());
                else
                    return Encoding.UTF8.GetString(data, 0, len);
            }
            catch (Exception)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "", CancellationToken.None);
                throw;
            }
        }

        public static Task SendString(this WebSocket webSocket, string text)
        {
            var senddata = Encoding.UTF8.GetBytes(text);
            return webSocket.SendAsync(new ArraySegment<byte>(senddata), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
