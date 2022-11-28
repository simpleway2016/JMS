using JMS.Common;
using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    public class NetClient:BaseNetClient
    {
        public const SslProtocols SSLProtocols = SslProtocols.Tls12;
        /// <summary>
        /// 启动GZip压缩的起点
        /// </summary>
        const int CompressionMinSize = 2048;
        public bool KeepAlive { get; set; }
      
        public NetClient PairClient { get; set; }
        public NetClient(){
            
        }
        public NetClient(Socket socket):base(socket)
        {
        }

        public NetClient(Stream stream) : base(stream)
        {

        }

        public override void Dispose()
        {
            if(PairClient != null)
            {
                PairClient.Dispose();
                PairClient = null;
            }
            base.Dispose();
        }

        /// <summary>
        /// 输出http，并等对方接收完毕
        /// </summary>
        /// <param name="contentBytes"></param>
        public void OutputHttpContent(byte[] contentBytes)
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {contentBytes.Length}\r\nConnection: keep-alive\r\n\r\n");
            this.Write(data);
            this.Write(contentBytes);
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

        /// <summary>
        /// 发送HealthyCheck命令，维持心跳
        /// </summary>
        public void KeepHeartBeating()
        {

            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(10000);
                    try
                    {
                        this.WriteServiceData(new GatewayCommand()
                        {
                            Type = CommandType.HealthyCheck
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

            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(10000);
                    try
                    {
                        this.WriteServiceData(cmdAction());
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

        public byte[] ReadServiceDataBytes(int flag)
        {
            if (flag == 1179010630)
                return new byte[0];

            var isgzip = (flag & 1) == 1;
            this.KeepAlive = (flag & 2) == 2;
            var len = flag >> 2;
            var datas = new byte[len];
            this.ReadData(datas, 0, len);
            if (isgzip)
                datas = GZipHelper.Decompress(datas);
            return datas;
        }

        public async Task<byte[]> ReadServiceDataBytesAsync(int flag)
        {
            if (flag == 1179010630)
                return new byte[0];

            var isgzip = (flag & 1) == 1;
            this.KeepAlive = (flag & 2) == 2;
            var len = flag >> 2;
            var datas = new byte[len];
            await this.ReadDataAsync(datas, 0, datas.Length);

            if (isgzip)
                datas = GZipHelper.Decompress(datas);
            return datas;
        }

        public byte[] ReadServiceDataBytes()
        {
            try
            {
                byte[] data = new byte[4];
                this.ReadData(data, 0, data.Length);
                var flag = BitConverter.ToInt32(data);
                return ReadServiceDataBytes(flag);
            }
            catch(System.IO.IOException ex)
            {
                if (ex.InnerException is SocketException)
                    throw ex.InnerException;
                throw ex;
            }
        }
        public int ReadInt()
        {
            byte[] data = new byte[4];
            this.ReadData(data, 0, data.Length);
            return BitConverter.ToInt32(data);
        }
        public async Task<int> ReadIntAsync()
        {
            byte[] data = new byte[4];
            await this.ReadDataAsync(data, 0, data.Length);
            return BitConverter.ToInt32(data);
        }
        public long ReadLong()
        {
            byte[] data = new byte[8];
            this.ReadData(data, 0, data.Length);
            return BitConverter.ToInt64(data);
        }
        public bool ReadBoolean()
        {
            return this.InnerStream.ReadByte() == 1;
        }

        public void Write(bool value)
        {
            this.InnerStream.WriteByte(value ? (byte)0x1:(byte)0x0);
        }

        /// <summary>
        /// 读取指定数量的数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public virtual async Task ReadDataAsync(byte[] data,int offset,int count)
        {
            int readed;
            while(count > 0)
            {
                readed = await this.InnerStream.ReadAsync(data, offset, count);
                if (readed <= 0)
                    throw new SocketException();
                count -= readed;
                offset += readed;
            }
        }

        public virtual void ReadData(byte[] data, int offset, int count)
        {
            int readed;
            while (count > 0)
            {
                readed = this.InnerStream.Read(data, offset, count);
                if (readed <= 0)
                    throw new SocketException();

                count -= readed;
                offset += readed;
            }
        }


        public async Task<byte[]> ReadServiceDataBytesAsync()
        {
            try
            {
                byte[] data = new byte[4];
                await this.ReadDataAsync(data, 0, data.Length);
                var flag = BitConverter.ToInt32(data);
                return await ReadServiceDataBytesAsync(flag);
            }
            catch (System.IO.IOException ex)
            {
                if (ex.InnerException is SocketException)
                    throw ex.InnerException;
                throw ex;
            }
        }

        public  string ReadServiceData()
        {
            var data = ReadServiceDataBytes();
            if (data.Length == 0)
                return null;
            return Encoding.UTF8.GetString(data);
        }

        public async Task<string> ReadServiceDataAsync()
        {
            var data = await ReadServiceDataBytesAsync();
            if (data.Length == 0)
                return null;
            return Encoding.UTF8.GetString(data);
        }

        public  T ReadServiceObject<T>()
        {
            var datas = ReadServiceDataBytes();
            string str = Encoding.UTF8.GetString(datas);
            try
            {
                return str.FromJson<T>();
            }
            catch (Exception ex)
            {
                throw new ConvertException(str, $"无法将{str}实例化为{typeof(T).FullName}，" + ex.ToString());
            }

        }

        public async Task<T> ReadServiceObjectAsync<T>()
        {
            var datas = await ReadServiceDataBytesAsync();
            string str = Encoding.UTF8.GetString(datas);
            try
            {
                return str.FromJson<T>();
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

            byte[] tosend = new byte[data.Length + 4];
            flag |= (data.Length << 2);

            fixed( byte* ptrData = data )
            {
                Marshal.Copy(new IntPtr(ptrData), tosend, 4, data.Length);
            }

            byte* ptr = (byte*)&flag;
            Marshal.Copy(new IntPtr(ptr), tosend, 0, 4);

            this.Write(tosend);
        }
        public  void WriteServiceData(object value)
        {
            if (value == null)
            {
                this.WriteServiceData(new byte[0]);
            }
            else
            {
                this.WriteServiceData(Encoding.UTF8.GetBytes(value.ToJsonString()));
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

            byte[] tosend = new byte[data.Length + 4];
            flag |= (data.Length << 2);

            fixed (byte* ptrData = data)
            {
                Marshal.Copy(new IntPtr(ptrData), tosend, 4, data.Length);
            }

            byte* ptr = (byte*)&flag;
            Marshal.Copy(new IntPtr(ptr), tosend, 0, 4);

            return this.InnerStream.WriteAsync(tosend,0,tosend.Length);
        }
        public Task WriteServiceDataAsync(object value)
        {
            if (value == null)
            {
                return this.WriteServiceDataAsync(new byte[0]);
            }
            else
            {
                return this.WriteServiceDataAsync(Encoding.UTF8.GetBytes(value.ToJsonString()));
            }

        }
    }
}
