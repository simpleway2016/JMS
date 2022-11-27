using JMS.Common;
using JMS.Dtos;
using System;
using System.Collections.Generic;
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
        public ProxyClient(NetAddress proxyAddr,X509Certificate2 cert) : base(cert)
        {
            this.ProxyAddress = proxyAddr;
        }

        public override void Connect(string address, int port)
        {
            if(this.ProxyAddress != null)
            {
                base.Connect(this.ProxyAddress.Address, this.ProxyAddress.Port);
                this.Address = address;
                this.Port = port;
                this.WriteServiceData(new NetAddress(address, port));
            }
            else
            {
                base.Connect(address, port);
            }
           
        }

        public override async Task ConnectAsync(string address, int port)
        {
            if (this.ProxyAddress != null)
            {
                await base.ConnectAsync(this.ProxyAddress.Address, this.ProxyAddress.Port);
                this.Address = address;
                this.Port = port;
                this.WriteServiceData(new NetAddress(address, port));
            }
            else
            {
                await base.ConnectAsync(address, port);
            }
        }

    }
}
