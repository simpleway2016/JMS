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

namespace JMS
{
    public static class NetClientExtensions
    {
        public static async Task ReadAndSendForLoop(this NetClient readClient, NetClient writeClient)
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

        public static async Task ReadAndSend(this NetClient readClient, NetClient writeClient, long totalLength)
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
              

        public static async Task<string> ReadHeaders(this PipeReader reader, IDictionary<string, string> headers)
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
               
                buffer = ret.Buffer;
                if (ret.IsCompleted)
                {
                    if (buffer.Length > 0)
                    {
                        reader.AdvanceTo(buffer.End);
                    }
                    throw new SocketException();
                }
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
                            while (requestPathLine.Contains("//"))
                            {
                                requestPathLine = requestPathLine.Replace("//", "/");
                            }
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


        public static async Task ReadAndSendForLoop(this Socket readSocket, Socket writeSocket, Action<byte[],int> callback = null)
        {
            int ret;
            int towrite;
            int offset;
            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            var data = new Memory<byte>(buffer);
            try
            {
                while (true)
                {
                    ret = await readSocket.ReceiveAsync(data, SocketFlags.None);
                    if(ret <= 0)
                    {
                        break;
                    }
                    callback?.Invoke(buffer, ret);
                    towrite = ret;
                    offset = 0;
                    while (towrite > 0)
                    {
                        ret = writeSocket.Send(buffer, offset , towrite, SocketFlags.None);
                        towrite -= ret;
                        offset += ret;
                    }
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                readSocket.Dispose();
                writeSocket.Dispose();
            }
        }
    }
}
