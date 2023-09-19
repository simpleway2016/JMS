using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
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

        NameValueCollection _RequestQuery;
        /// <summary>
        /// 客户端请求的Query
        /// </summary>
        public NameValueCollection RequestQuery
        {
            get
            {
                if (_RequestQuery == null && RequestingObject.Value != null)
                {
                    _RequestQuery = RequestingObject.Value.RequestQuery;
                }
                return _RequestQuery;
            }
        }

        public string SubProtocol
        {
            get;
            internal set;
        }

        public abstract Task OnConnected(WebSocket webSocket);
    }

    public static class WebSocketExtens
    {
        public static async Task<string> ReadString(this WebSocket webSocket)
        {
            byte[] data = ArrayPool<byte>.Shared.Rent(4096);
            List<byte> list = null;
            try
            {
                var buffer = new ArraySegment<byte>(data);
                int len;
                while (true)
                {
                    var ret = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    if (ret.CloseStatus != null)
                    {
                        throw new WebSocketException(ret.CloseStatusDescription);
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
                    else if(len > 0)
                    {
                        if (list == null)
                            list = new List<byte>();
                        list.AddRange(buffer.Slice(0, ret.Count));
                        if (list.Count > 102400)
                        {
                            list.Clear();
                            throw new Exception("websocket data is too big");
                        }
                    }
                }
                if (list != null)
                    return Encoding.UTF8.GetString(list.ToArray());
                else
                    return Encoding.UTF8.GetString(data, 0, len);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
                list?.Clear();
            }
        }

        public static Task SendString(this WebSocket webSocket, string text)
        {
            var senddata = Encoding.UTF8.GetBytes(text);
            return webSocket.SendAsync(new ArraySegment<byte>(senddata), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
