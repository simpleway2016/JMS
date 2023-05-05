using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Buffers;
using System.IO.Pipelines;
using System.Reflection.PortableExecutable;
using JMS.Common;

namespace JMS.ServerCore
{
    public class HttpHelper
    {
        public static async Task ReadAndSendForLoop(NetClient readClient, NetClient writeClient)
        {
            ReadResult ret;
            ReadOnlySequence<byte> buffer;
            try
            {
                while (true)
                {
                    ret = await readClient.PipeReader.ReadAsync();
                    buffer = ret.Buffer;
                   

                    if (buffer.IsSingleSegment)
                    {
                        writeClient.InnerStream.Write(buffer.First.Span);
                    }
                    else
                    {
                        foreach (ReadOnlyMemory<byte> memory in buffer)
                        {
                            writeClient.InnerStream.Write(memory.Span);
                        }
                    }

                    // 告诉PipeReader已经处理多少缓冲
                    readClient.PipeReader.AdvanceTo(buffer.End);

                    if (ret.IsCompleted)
                    {
                        return;
                    }

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

        public static async Task ReadAndSend(NetClient readClient, NetClient writeClient, long totalLength)
        {
            ReadResult ret;
            ReadOnlySequence<byte> buffer;
            while (totalLength > 0)
            {
                ret = await readClient.PipeReader.ReadAsync();
                buffer = ret.Buffer;

                if(buffer.Length > totalLength)
                {
                    buffer = buffer.Slice(0, totalLength);
                }

                totalLength -= buffer.Length;


               

                if (buffer.IsSingleSegment)
                {
                    writeClient.InnerStream.Write(buffer.First.Span);
                }
                else
                {
                    foreach (ReadOnlyMemory<byte> memory in buffer)
                    {
                        writeClient.InnerStream.Write(memory.Span);
                    }
                }

                // 告诉PipeReader已经处理多少缓冲
                readClient.PipeReader.AdvanceTo(buffer.End);

                if (ret.IsCompleted && totalLength > 0)
                {
                    throw new SocketException();
                }
            }
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

        public static async Task<string> ReadHeaders(PipeReader reader, IDictionary<string, string> headers)
        {

            ReadResult ret;
            SequencePosition? position;
            int indexFlag;
            string line;
            const byte n = (byte)'\n';
            const byte r = (byte)'\r';
            ReadOnlySequence<byte> block;
            ReadOnlySequence<byte> buffer;
            string requestPathLine = null;
            while (true)
            {
                ret = await reader.ReadAsync();
                if (ret.IsCompleted)
                {
                    throw new SocketException();
                }
                buffer = ret.Buffer;

                do
                {
                    position = buffer.PositionOf(n);
                    if (position != null)
                    {
                        block = buffer.Slice(0, position.Value);
                        line = block.GetString();                     

                        // 往position位置偏移1个字节
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));

                        if (block.Length == 0 || (block.Length == 1 && block.First.Span[0] == r))
                        {                            
                            // 告诉PipeReader已经处理多少缓冲
                            reader.AdvanceTo(buffer.Start);
                            return requestPathLine;
                        }

                        if (requestPathLine == null)
                        {
                            requestPathLine = line.Trim();
                            continue;
                        }
                        else if ((indexFlag = line.IndexOf(':', 0)) > 0 && indexFlag < line.Length - 1)
                        {
                            var key = line.Substring(0, indexFlag);
                            var value = line.Substring(indexFlag + 1).Trim();
                            headers[key] = value;
                            if (headers.Count > 100)
                                throw new SizeLimitException("too many header keys");
                        }
                    }
                    else
                    {
                        if (buffer.Length > 10240)
                            throw new SizeLimitException("header too big");
                    }
                }
                while (position != null);

              

                // 告诉PipeReader已经处理多少缓冲
                reader.AdvanceTo(buffer.Start,buffer.End);

            }
        }

        /// <summary>
        /// 解析http请求头
        /// </summary>
        /// <param name="preRequestString"></param>
        /// <param name="stream"></param>
        /// <param name="headers"></param>
        /// <returns>请求的第一行语句</returns>
        /// <exception cref="SocketException"></exception>
        static async Task<string> ReadHeaders(string preRequestString, Stream stream, IDictionary<string, string> headers)
        {
            List<byte> lineBuffer = new List<byte>(1024);
            string line = null;
            string requestPathLine = null;
            byte[] bData = new byte[1];
            int readed;
            int indexFlag;
            while (true)
            {
                readed = await stream.ReadAsync(bData, 0, 1);
                if (readed <= 0)
                    throw new SocketException();

                if (bData[0] == 10)
                {
                    if (lineBuffer.Count == 0)
                        break;

                    line = Encoding.UTF8.GetString(lineBuffer.ToArray());
                    lineBuffer.Clear();
                    if (requestPathLine == null)
                        requestPathLine = preRequestString + line;
                    else if ((indexFlag = line.IndexOf(':', 0)) > 0 && indexFlag < line.Length - 1)
                    {
                        var key = line.Substring(0, indexFlag);
                        var value = line.Substring(indexFlag + 1).Trim();
                        headers[key] = value;
                        if (headers.Count > 100)
                            throw new Exception("too many header keys");
                    }
                }
                else if (bData[0] != 13)
                {
                    lineBuffer.Add(bData[0]);
                    if (lineBuffer.Count > 10240)
                        throw new Exception("header too big");
                }
            }
            return requestPathLine;
        }
    }
}
