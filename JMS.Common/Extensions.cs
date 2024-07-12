using JMS.Common;
using Microsoft.Extensions.Configuration;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Way.Lib;

namespace JMS
{
    public static class Extensions
    {
        public static string[] GetStringArrayParameters(this object[] source)
        {
            if (source == null)
                return null;

            string[] ret = new string[source.Length];
            for(int i = 0; i < source.Length; i ++)
            {
                ret[i] = source[i].ToJsonString();
            }
            return ret;
        }

        /// <summary>
        /// 和Get方法类似，此方法返回的对象，会自动随着配置文件内容变更而更新
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static ConfigurationValue<T> GetNewest<T>(this IConfiguration configuration)
        {
            var obj = new ConfigurationValue<T>(configuration);
            return obj;
        }
        
        public static string GetString(this ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                return Encoding.UTF8.GetString(buffer.First.Span);
            }
            else
            {
                var len = (int)buffer.Length;
                using (var data = MemoryPool<byte>.Shared.Rent(len))
                {
                    Memory<byte> memory = data.Memory;

                    foreach (var block in buffer)
                    {
                        block.CopyTo(memory);
                        memory = memory.Slice(block.Length);
                    }
                    return Encoding.UTF8.GetString(data.Memory.Slice(0 , len).Span);
                }
            }
        }
    }
}

public static class WebSocketExtens
{
    public static Task<string> ReadString(this WebSocket webSocket)
    {
        return ReadString(webSocket, CancellationToken.None);
    }

    public static async Task<string> ReadString(this WebSocket webSocket, CancellationToken cancellationToken)
    {
        byte[] data = ArrayPool<byte>.Shared.Rent(4096);
        List<byte> list = null;
        try
        {
            var buffer = new ArraySegment<byte>(data);
            int len;
            while (true)
            {
                var ret = await webSocket.ReceiveAsync(buffer, cancellationToken);
                if (ret.Count <= 0 && ret.CloseStatus != null)
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
                else if (len > 0)
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

    public static async Task<byte[]> ReadBinary(this WebSocket webSocket, CancellationToken cancellationToken)
    {
        byte[] data = ArrayPool<byte>.Shared.Rent(4096);
        List<byte> list = null;
        try
        {
            var buffer = new ArraySegment<byte>(data);
            int len;
            while (true)
            {
                var ret = await webSocket.ReceiveAsync(buffer, cancellationToken);
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
                else if (len > 0)
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
                return list.ToArray();
            else
                return buffer.Slice(0, len).ToArray();
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
    public static Task SendString(this WebSocket webSocket, string text, CancellationToken cancellationToken)
    {
        var senddata = Encoding.UTF8.GetBytes(text);
        return webSocket.SendAsync(new ArraySegment<byte>(senddata), WebSocketMessageType.Text, true, cancellationToken);
    }
    public static Task SendBinary(this WebSocket webSocket, byte[] data, int offset, int count, CancellationToken cancellationToken)
    {
        return webSocket.SendAsync(new ArraySegment<byte>(data, offset, count), WebSocketMessageType.Text, true, cancellationToken);
    }
}
