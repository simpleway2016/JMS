using JMS.Common;
using JMS.Common.Net;
using JMS.Dtos;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    /// <summary>
    /// 通过代理访问指定服务器
    /// </summary>
    public class ProxyClient : CertClient
    {
        public NetAddress ProxyAddress { get; }
        NetAddress _netaddr;
        public override NetAddress NetAddress => _netaddr;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="proxyAddr">代理地址，如果为null，则直接访问目标服务器</param>
        public ProxyClient(NetAddress proxyAddr)
        {
            this.ProxyAddress = proxyAddr;
        }

        public override void Connect(NetAddress addr)
        {
            _netaddr = addr;
            if (this.ProxyAddress != null)
            {
                base.Connect(this.ProxyAddress);               
            }
            else
            {
                base.Connect(addr);
            }
        }

        public override async Task ConnectAsync(NetAddress addr)
        {
            _netaddr = addr;
            if (this.ProxyAddress != null)
            {              
                await base.ConnectAsync(this.ProxyAddress).ConfigureAwait(false);
            }
            else
            {
                await base.ConnectAsync(addr).ConfigureAwait(false);
            }
        }


        protected override void AfterConnect()
        {
            base.AfterConnect();

            if (this.ProxyAddress != null)
            {
                //发送socks5协议
                byte[] buffer = ArrayPool<byte>.Shared.Rent(128);
                try
                {
                    buffer[0] = 0x5;
                    buffer[1] = 0x1;
                    buffer[2] = 0x0;
                    this.InnerStream.Write(buffer, 0, 3);

                    this.ReadData(buffer, 0, 2);

                    byte[] addrBytes = null;
                    bool isdomain = false;
                    byte addrType;
                    if (IPAddress.TryParse(_netaddr.Address, out IPAddress ip))
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
                        addrBytes = Encoding.ASCII.GetBytes(_netaddr.Address);
                    }


                    byte[] portBytes = BitConverter.GetBytes((ushort)_netaddr.Port);

                    var len = 0;
                    buffer[0] = 0x5;
                    buffer[1] = 0x1;
                    buffer[2] = 0x0;
                    buffer[3] = addrType;
                    if (isdomain == false)
                    {
                        Array.Copy(addrBytes, 0, buffer, 4, addrBytes.Length);
                        buffer[4 + addrBytes.Length] = portBytes[1];
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
                        throw new ProxyException($"{_netaddr}代理服务器不能转发");
                    }
                    if (buffer[3] == 0x1)
                    {
                        this.ReadData(buffer, 0, 6);
                    }
                    else if (buffer[3] == 0x3)
                    {
                        len = this.InnerStream.ReadByte();
                        if (len < 0)
                            throw new SocketException();
                        this.ReadData(buffer, 0, len + 2);
                    }
                    else if (buffer[3] == 0x4)
                    {
                        this.ReadData(buffer, 0, 18);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

    }
}
