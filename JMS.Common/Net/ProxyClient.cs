using JMS.Common;
using JMS.Common.Net;
using JMS.Dtos;
using Org.BouncyCastle.Bcpg;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    /// <summary>
    /// 通过代理访问指定服务器
    /// </summary>
    public class ProxyClient : CertClient
    {
        public NetAddress ProxyAddress { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="proxyAddr">代理地址，如果为null，则直接访问目标服务器</param>
        /// <param name="cert">访问代理服务器的客户端证书</param>
        public ProxyClient(NetAddress proxyAddr, X509Certificate2 cert) : base(cert)
        {
            this.ProxyAddress = proxyAddr;
        }

        public override void Connect(string address, int port)
        {
            if (this.ProxyAddress != null)
            {
                _addr = address;
                _port = port;
                base.Connect(this.ProxyAddress.Address, this.ProxyAddress.Port);
                this.Address = address;
                this.Port = port;
            }
            else
            {
                base.Connect(address, port);
            }

        }


        string _addr;
        int _port;
        public override async Task ConnectAsync(string address, int port)
        {
            if (this.ProxyAddress != null)
            {
                _addr = address;
                _port = port;
                await base.ConnectAsync(this.ProxyAddress.Address, this.ProxyAddress.Port);
                this.Address = address;
                this.Port = port;
            }
            else
            {
                await base.ConnectAsync(address, port);
            }
        }

        protected override void AfterConnect()
        {
            base.AfterConnect();

            if (this.ProxyAddress != null)
            {
                //发送socks5协议
                byte[] buffer = new byte[128];
                buffer[0] = 0x5;
                buffer[1] = 0x1;
                buffer[2] = 0x0;
                this.InnerStream.Write(buffer, 0, 3);

                this.ReadData(buffer, 0, 2);

                byte[] addrBytes = null;
                bool isdomain = false;
                byte addrType;
                if (IPAddress.TryParse(_addr, out IPAddress ip))
                {
                    addrBytes = ip.GetAddressBytes();
                    if (addrBytes.Length > 4)
                    {
                        addrType = 0x4;//ipv6
                    }
                    else
                    {
                        addrType = 0x1;//ipv4
                    }
                }
                else
                {
                    isdomain = true;
                    addrType = 0x3;//域名
                    addrBytes = Encoding.ASCII.GetBytes(_addr);
                }


                byte[] portBytes = BitConverter.GetBytes((ushort)_port);

                var len = 0;
                buffer[0] = 0x5;
                buffer[1] = 0x1;
                buffer[2] = 0x0;
                buffer[3] = addrType;
                if (isdomain == false)
                {
                    Array.Copy(addrBytes, 0, buffer, 4, addrBytes.Length);
                    buffer[4+ addrBytes.Length] = portBytes[1];
                    buffer[4 + addrBytes.Length + 1] = portBytes[0];
                    len = 4 + addrBytes.Length + 2;
                }
                else
                {
                    buffer[4] = (byte)addrBytes.Length;
                    Array.Copy(addrBytes, 0, buffer, 5, addrBytes.Length);
                    buffer[5 + addrBytes.Length] = portBytes[1];
                    buffer[5 + addrBytes.Length + 1] = portBytes[0];
                    len = 5 + addrBytes.Length + 2;
                }

                this.InnerStream.Write(buffer, 0, len);

                this.ReadData(buffer, 0, 4);
                if (buffer[1] != 0)
                {
                    throw new ProxyException($"{_addr}:{_port}代理服务器不能转发");
                }
                if (buffer[3] == 0x1)
                {
                    this.ReadData(buffer, 0, 6);
                }
                else if (buffer[3] == 0x3)
                {
                    len = this.InnerStream.ReadByte();
                    this.ReadData(buffer, 0, len + 2);
                }
                else if (buffer[3] == 0x4)
                {
                    this.ReadData(buffer, 0, 18);
                }
            }
        }

    }
}
