using JMS.Common;
using JMS.Common.Dtos;
using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    public class NetClient:Way.Lib.NetStream
    {
        /// <summary>
        /// 启动GZip压缩的起点
        /// </summary>
        const int CompressionMinSize = 2048;
        public bool KeepAlive { get; set; }
        public string Address { get; private set; }
        public int Port { get; private set; }
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

        public  void WriteServiceData(byte[] data)
        {
            if (data.Length > CompressionMinSize)
            {
                data = GZipHelper.Compress(data);
                int len = (data.Length << 2) | 1;//第一位表示gzip，第二位表示keepclient
                if (KeepAlive)
                    len |= 2;
                this.Write(len);
                this.Write(data);
            }
            else
            {
                int len = (data.Length << 2);
                if (KeepAlive)
                    len |= 2;
                this.Write(len);
                this.Write(data);
            }
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
