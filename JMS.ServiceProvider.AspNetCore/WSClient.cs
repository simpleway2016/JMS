using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace JMS.ServiceProvider.AspNetCore
{
    public class WSClient:IDisposable
    {
        WebSocket _webSocket;
        public WSClient(WebSocket webSocket)
        {
            this._webSocket = webSocket;

        }

        public void Close(WebSocketCloseStatus status , string reason)
        {
            _webSocket.CloseAsync(status, reason, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

        }

        public void SendData(string data)
        {
            var senddata = Encoding.UTF8.GetBytes(data);
            _webSocket.SendAsync(new ArraySegment<byte>(senddata), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

        }
        public string ReceiveData()
        {
            List<byte> list = null;
            byte[] data = new byte[4096];
            var buffer = new ArraySegment<byte>(data);
            int len;
            while (true)
            {
                var ret = _webSocket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                len = ret.Count;
                if(len == 0)
                {
                    if(_webSocket.State != WebSocketState.Open)
                    {
                        throw new WebSocketException("websocket is not open,reason:" + _webSocket.CloseStatusDescription);
                    }
                }
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

        public void Dispose()
        {
            _webSocket.Dispose();
        }
    }
}
