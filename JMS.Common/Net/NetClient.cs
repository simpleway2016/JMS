using JMS.Common.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Way.Lib;

namespace JMS
{
    public class NetClient:Way.Lib.NetStream
    {
        public NetClient(NetAddress addr) : base(addr.Address, addr.Port)
        {
            this.ReadTimeout = 16000;
        }
        public NetClient(string ip,int port) : base(ip,port)
        {
            this.ReadTimeout = 16000;
        }
        public NetClient(Socket socket):base(socket)
        {
            this.ReadTimeout = 16000;
        }

        public  string ReadServiceData()
        {
            var len = this.ReadInt();
            var data = this.ReceiveDatas(len);
            if (data.Length == 0)
                return null;
            return Encoding.UTF8.GetString(data);
        }

        public  T ReadServiceObject<T>()
        {
            var len = this.ReadInt();
            var datas = this.ReceiveDatas(len);
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
            this.Write(data.Length);
            this.Write(data);
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
