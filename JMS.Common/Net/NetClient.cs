﻿using JMS.Common;
using JMS.Common.Json;
using JMS.Dtos;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    public class NetClient : BaseNetClient
    {
        /// <summary>
        /// 启动GZip压缩的起点
        /// </summary>
        const int CompressionMinSize = 2048;
        /// <summary>
        /// 协议包最大尺寸，默认102400
        /// </summary>
        public int MaxCommandSize = 102400;
        public bool KeepAlive { get; set; }

        public NetClient()
        {

        }
        public NetClient(Socket socket) : base(socket)
        {
        }

        public NetClient(Stream stream) : base(stream)
        {

        }


        /// <summary>
        /// 输出http，并等对方接收完毕
        /// </summary>
        /// <param name="contentBytes"></param>
        public void OutputHttpContent(byte[] contentBytes)
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {contentBytes.Length}\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data.Concat(contentBytes).ToArray());
        }

        public void OutputHttp204(IDictionary<string, string> requestHeaders)
        {
            StringBuilder otherHeaders = new StringBuilder();
            if (requestHeaders.ContainsKey("Access-Control-Request-Headers"))
                otherHeaders.Append($"Access-Control-Allow-Headers: {requestHeaders["Access-Control-Request-Headers"]}\r\n");

            if (requestHeaders.ContainsKey("Access-Control-Request-Method"))
                otherHeaders.Append($"Access-Control-Allow-Methods: {requestHeaders["Access-Control-Request-Method"]}\r\n");

            if (requestHeaders.ContainsKey("Origin"))
                otherHeaders.Append($"Access-Control-Allow-Origin: {requestHeaders["Origin"]}\r\nVary: Origin\r\n");
            var body = $"HTTP/1.1 204 No Content\r\nAccess-Control-Allow-Credentials: true\r\n{otherHeaders}\r\n";
            var data = System.Text.Encoding.UTF8.GetBytes(body);
            this.Write(data);
        }

        /// <summary>
        /// 输出重定向头，并等对方接收完毕
        /// </summary>
        /// <param name="location"></param>
        public void OutputHttpRedirect301(string location)
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 301 Moved Permanently\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            this.Write(data);
        }

       
        /// <summary>
        /// 从socket中预读取一定的字节数据
        /// </summary>
        /// <param name="count"></param>
        /// <returns>如果返回null，则网络已中断</returns>
        public async Task<string> PreReadBytesAsync(int count)
        {
            int i;
            byte[] data = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                while (true)
                {
                    var readed = await this.Socket.ReceiveAsync(data, SocketFlags.Peek).ConfigureAwait(false);
                    if (readed <= 0)
                        return null;

                    if (readed >= count)
                    {
                        return Encoding.UTF8.GetString(data, 0, count);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }

        }
        /// <summary>
        /// 输出重定向头，并等对方接收完毕
        /// </summary>
        /// <param name="location"></param>
        public void OutputHttpRedirect(string location)
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 302 Found\r\nLocation: {location}\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: 0\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
        }

        public void OutputHttpNotFund()
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 404 NotFund\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: 0\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
        }

        public void OutputHttp401()
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 401 NotAllow\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: 0\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
        }

        public void OutputHttpCode(int code, string desc)
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 {code} {desc}\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: 0\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
        }
        public void OutputNotModified()
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 304 Not Modified\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
        }
        public void OutputHttp500(string message)
        {
            var content = message == null ? null : Encoding.UTF8.GetBytes(message);
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 500 Error\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {(content == null ? 0 : content.Length)}\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
            if (content != null)
            {
                this.Write(content);
            }
        }

        public void OutputHttpCode(int code, string desc, string message)
        {
            var content = message == null ? null : Encoding.UTF8.GetBytes(message);
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 {code} {desc}\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {(content == null ? 0 : content.Length)}\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
            if (content != null)
            {
                this.Write(content);
            }
        }

        public void OutputHttpCodeAndClose(int code, string desc, string message)
        {
            var content = message == null ? null : Encoding.UTF8.GetBytes(message);
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 {code} {desc}\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {(content == null ? 0 : content.Length)}\r\nConnection: close\r\n\r\n");
            this.Write(data);
            if (content != null)
            {
                this.Write(content);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="contentType"></param>
        /// <param name="headers">输入更多header,多个header用\r\n隔开，如: Name: Jack\r\nId: 1</param>
        public void OutputHttp200(string message, string contentType = "text/html", string headers = null)
        {
            byte[] content = message == null ? null : Encoding.UTF8.GetBytes(message);
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\n{headers}{(headers != null ? "\r\n" : "")}Content-Type: {contentType}; charset=utf-8\r\nContent-Length: {(content == null ? 0 : content.Length)}\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
            if (content != null)
            {
                this.Write(content);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="contentType"></param>
        /// <param name="headers">输入更多header,多个header用\r\n隔开，如: Name: Jack\r\nId: 1</param>
        public void OutputHttpGzip200(string message, string contentType = "text/html", string headers = null)
        {
            byte[] content = message == null ? null : Encoding.UTF8.GetBytes(message);

            OutputHttpGzip200(content, contentType, headers);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <param name="headers">输入更多header,多个header用\r\n隔开，如: Name: Jack\r\nId: 1</param>
        public void OutputHttpGzip200(byte[] content, string contentType = "text/html", string headers = null)
        {
            if (content != null)
            {
                content = GZipHelper.Compress(content);
            }

            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nAccess-Control-Allow-Origin: *\r\n{headers}{(headers != null ? "\r\n" : "")}Content-Type: {contentType}; charset=utf-8\r\nContent-Length: {(content == null ? 0 : content.Length)}\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
            if (content != null)
            {
                this.Write(content);
            }
        }

        /// <summary>
        /// 发送HealthyCheck命令，维持心跳
        /// </summary>
        public void KeepHeartBeating()
        {

            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(10000);
                    try
                    {
                        this.WriteServiceData(new GatewayCommand()
                        {
                            Type = (int)CommandType.HealthyCheck
                        });
                    }
                    catch (Exception ex)
                    {
                        return;
                    }
                }
            });

            this.ReadTimeout = 30000;
            while (true)
            {
                try
                {
                    this.ReadServiceObject<InvokeResult>();
                }
                catch
                {
                    return;
                }
            }
        }


        /// <summary>
        /// 发送特定命令，维持心跳
        /// </summary>
        /// <param name="cmdAction"></param>
        public void KeepHeartBeating(Func<object> cmdAction)
        {
            bool exited = false;
            Task.Run(async () =>
            {
                await Task.Delay(10000).ConfigureAwait(false);
                while (!exited && !Disposed)
                {                  
                    try
                    {
                        this.WriteServiceData(cmdAction());
                        await Task.Delay(10000).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return;
                    }
                }
            });

            this.ReadTimeout = 30000;
            while (true)
            {
                try
                {
                    this.ReadServiceObject<InvokeResult>();
                }
                catch
                {
                    exited = true;
                    return;
                }
            }
        }


        async Task<byte[]> readBytesByFlagAsync(int flag)
        {
            if (flag == 1179010630)
                return new byte[0];

            var isgzip = (flag & 1) == 1;
            this.KeepAlive = (flag & 2) == 2;
            var len = flag >> 2;

            if (len > MaxCommandSize)
                throw new SizeLimitException("command size is too big");
            var ret = await this.PipeReader.ReadAtLeastAsync(len).ConfigureAwait(false);
            if (ret.IsCompleted && ret.Buffer.Length < len)
                throw new SocketException();

            var buffer = ret.Buffer.Slice(0, len);

            var datas = buffer.ToArray();
            this.PipeReader.AdvanceTo(buffer.End);

            if (isgzip)
                datas = GZipHelper.Decompress(datas);
            return datas;
        }

        byte[] readBytesByFlag(int flag)
        {
            if (flag == 1179010630)
                return new byte[0];

            var isgzip = (flag & 1) == 1;
            this.KeepAlive = (flag & 2) == 2;
            var len = flag >> 2;

            if (len > MaxCommandSize)
                throw new SizeLimitException("command size is too big");

            var datas = new byte[len];
            this.ReadData(datas, 0, len);

            if (isgzip)
                datas = GZipHelper.Decompress(datas);
            return datas;
        }

        public int ReadInt()
        {
            var data = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                this.ReadData(data, 0, 4);
                return BitConverter.ToInt32(data);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }
        public async Task<int> ReadIntAsync()
        {
            var data = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                await this.ReadDataAsync(data, 0, 4).ConfigureAwait(false);
                return BitConverter.ToInt32(data);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }

        public bool ReadBoolean()
        {
            return this.InnerStream.ReadByte() == 1;
        }

        public async Task<bool> ReadBooleanAsync()
        {
            var data = ArrayPool<byte>.Shared.Rent(1);
            try
            {
                await this.ReadDataAsync(data, 0, 1).ConfigureAwait(false);
                return data[0] == 1;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }

        public long ReadLong()
        {
            var data = ArrayPool<byte>.Shared.Rent(8);
            try
            {
                this.ReadData(data, 0, 8);
                return BitConverter.ToInt64(data);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }

        public async Task<long> ReadLongAsync()
        {
            var data = ArrayPool<byte>.Shared.Rent(8);
            try
            {
                await this.ReadDataAsync(data, 0, 8).ConfigureAwait(false);
                return BitConverter.ToInt64(data);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }

        public void Write(bool value)
        {
            this.InnerStream.WriteByte(value ? (byte)0x1 : (byte)0x0);
        }

        /// <summary>
        /// 读取指定数量的数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public virtual async Task ReadDataAsync(byte[] data, int offset, int count)
        {
            var ret = await this.PipeReader.ReadAtLeastAsync(count).ConfigureAwait(false);
            if (ret.IsCompleted && ret.Buffer.Length < count)
                throw new SocketException();

            var buffer = ret.Buffer.Slice(0, count);
            if (data != null)
            {
                buffer.CopyTo(new Span<byte>(data, offset, count));
            }
            this.PipeReader.AdvanceTo(buffer.End);

        }

        public virtual void ReadData(byte[] data, int offset, int count)
        {
            int readed;
            while (count > 0)
            {
                readed = this.InnerStream.Read(data, offset, count);
                if (readed <= 0)
                    throw new SocketException();
                offset += readed;
                count -= readed;
            }
        }

        public byte[] ReadServiceDataBytes()
        {
            try
            {
                var flag = this.ReadInt();

                return readBytesByFlag(flag);
            }
            catch (System.IO.IOException ex)
            {
                if (ex.InnerException is SocketException)
                    throw ex.InnerException;
                throw ex;
            }
        }

        public async Task<byte[]> ReadServiceDataBytesAsync()
        {
            try
            {
                var ret = await this.PipeReader.ReadAtLeastAsync(4).ConfigureAwait(false);
                if (ret.IsCompleted && ret.Buffer.Length < 4)
                    throw new SocketException();

                var buffer = ret.Buffer.Slice(0, 4);
                var flag = BitConverter.ToInt32(buffer.First.Span);
                this.PipeReader.AdvanceTo(buffer.End);

                return await readBytesByFlagAsync(flag).ConfigureAwait(false);
            }
            catch (System.IO.IOException ex)
            {
                if (ex.InnerException is SocketException)
                    throw ex.InnerException;
                throw ex;
            }
        }

        public string ReadServiceData()
        {
            var data = ReadServiceDataBytes();
            if (data.Length == 0)
                return null;
            return Encoding.UTF8.GetString(data);
        }

        public async Task<string> ReadServiceDataAsync()
        {
            var data = await ReadServiceDataBytesAsync().ConfigureAwait(false);
            if (data.Length == 0)
                return null;
            return Encoding.UTF8.GetString(data);
        }

        public T ReadServiceObject<T>()
        {
            var datas = ReadServiceDataBytes();
            string str = Encoding.UTF8.GetString(datas);
            try
            {
                return ApplicationJsonSerializer.JsonSerializer.Deserialize<T>(str);
            }
            catch (Exception ex)
            {
                throw new ConvertException(str, $"无法将{str}实例化为{typeof(T).FullName}，" + ex.ToString());
            }

        }



        public async Task<T> ReadServiceObjectAsync<T>()
        {
            var datas = await ReadServiceDataBytesAsync().ConfigureAwait(false);
            string str = Encoding.UTF8.GetString(datas);
            try
            {
                return ApplicationJsonSerializer.JsonSerializer.Deserialize<T>(str);
            }
            catch (Exception ex)
            {
                throw new ConvertException(str, $"无法将{str}实例化为{typeof(T).FullName}，" + ex.ToString());
            }

        }

        public unsafe void WriteServiceData(byte[] data)
        {
            //第一位表示gzip，第二位表示keepclient
            int flag = 0;

            if (data.Length > CompressionMinSize)
            {
                data = GZipHelper.Compress(data);
                flag = 1;
            }

            if (KeepAlive)
                flag |= 2;

            var newLen = data.Length + 4;
            byte[] tosend = ArrayPool<byte>.Shared.Rent(newLen);
            try
            {
                flag |= (data.Length << 2);

                fixed (byte* ptrData = data)
                {
                    Marshal.Copy(new IntPtr(ptrData), tosend, 4, data.Length);
                }

                byte* ptr = (byte*)&flag;
                Marshal.Copy(new IntPtr(ptr), tosend, 0, 4);

                this.InnerStream.Write(tosend, 0, newLen);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tosend);
            }
        }
        public void WriteServiceData(object value)
        {
            if (value == null)
            {
                this.WriteServiceData(new byte[0]);
            }
            else
            {
                this.WriteServiceData(Encoding.UTF8.GetBytes(ApplicationJsonSerializer.JsonSerializer.Serialize(value)));
            }

        }

        public unsafe Task WriteServiceDataAsync(byte[] data)
        {
            //第一位表示gzip，第二位表示keepclient
            int flag = 0;

            if (data.Length > CompressionMinSize)
            {
                data = GZipHelper.Compress(data);
                flag = 1;
            }

            if (KeepAlive)
                flag |= 2;

            var newLen = data.Length + 4;
            byte[] tosend = ArrayPool<byte>.Shared.Rent(newLen);
            try
            {
                flag |= (data.Length << 2);

                fixed (byte* ptrData = data)
                {
                    Marshal.Copy(new IntPtr(ptrData), tosend, 4, data.Length);
                }

                byte* ptr = (byte*)&flag;
                Marshal.Copy(new IntPtr(ptr), tosend, 0, 4);

                return this.InnerStream.WriteAsync(tosend, 0, newLen);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tosend);
            }
        }
        public Task WriteServiceDataAsync(object value)
        {
            if (value == null)
            {
                return this.WriteServiceDataAsync(new byte[0]);
            }
            else
            {
                return this.WriteServiceDataAsync(Encoding.UTF8.GetBytes(ApplicationJsonSerializer.JsonSerializer.Serialize(value)));
            }

        }

        static byte[] CheckBs = new byte[1];
        /// <summary>
        /// 检查健康状态
        /// </summary>
        internal async void checkStatus()
        {
            try
            {
                //设置超时
                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(NetClientPool.RELEASESECONDS)))
                {
                    //不要读取Memory<byte>.Empty，因为会返回0，无法判断是否网络断开
                    var count = await this.Socket.ReceiveAsync(CheckBs, SocketFlags.Peek, cancellation.Token).ConfigureAwait(false);
                    if (count == 0)
                    {
                        this.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                this.Dispose();
            }
        }
    }
}
