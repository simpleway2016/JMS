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
        /// <param name="targetAddr">目标服务器地址</param>
        /// <param name="cert">访问代理服务器的客户端证书</param>
        public ProxyClient(NetAddress proxyAddr,NetAddress targetAddr, X509Certificate2 cert) : base(proxyAddr == null ? targetAddr:proxyAddr, cert)
        {
            this.ProxyAddress = proxyAddr;
            this.Address = targetAddr.Address;
            this.Port = targetAddr.Port;
            if(proxyAddr != null)
                this.WriteServiceData(targetAddr);
        }
       
    }
}
