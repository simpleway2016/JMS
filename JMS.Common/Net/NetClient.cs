using JMS.Common;
using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    public class NetClient:Way.Lib.NetStream
    {
        public const SslProtocols SSLProtocols = SslProtocols.Tls12;
        /// <summary>
        /// 启动GZip压缩的起点
        /// </summary>
        const int CompressionMinSize = 2048;
        public bool KeepAlive { get; set; }
        public string Address { get; protected set; }
        public int Port { get; protected set; }
        public NetClient(NetAddress addr) : base(addr.Address, addr.Port)
        {
            this.Address = addr.Address;
            this.Port = addr.Port;
            this.ReadTimeout = 16000;
        }
        public NetClient(string ip,int port) : base(ip,port)
        {
            this.Address = ip;
            this.Port = port;
            this.ReadTimeout = 16000;
        }
        public NetClient(Socket socket):base(socket)
        {
            this.ReadTimeout = 16000;
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

        public byte[] ReadServiceDataBytes()
        {
            try
            {
                var flag = this.ReadInt();
                if (flag == 1179010630)
                    return new byte[0];

                var isgzip = (flag & 1) == 1;
                this.KeepAlive = (flag & 2) == 2;
                var len = flag >> 2;
                var datas = this.ReceiveDatas(len);
                if (isgzip)
                    datas = GZipHelper.Decompress(datas);
                return datas;
            }
            catch(System.IO.IOException ex)
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
                throw new ConvertException(str, $"无法将{str}实例化为{typeof(T).FullName}");
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
    }
}
