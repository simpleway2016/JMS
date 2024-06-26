using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1.IsisMtt.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Dtos
{

    public class AppConfig
    {
        public bool LogDetails { get; set; }
        public DeviceConfig[] Devices { get; set; }
        public ServerConfig[] Servers { get; set; }
    }

    public class DeviceConfig
    {
        public string Name { get; set; }
        public string Password { get; set; }
    }

    public class ServerConfig
    {
        public ProxyType Type { get; set; }
        public string[] ProxyIps { get; set; }
        public SslConfig SSL { get; set; }
        public int Port { get; set; }
        public ProxyConfig[] Proxies { get; set; }
    }

    public class SslConfig
    {
        public string Cert { get; set; }
        public string Password { get; set; }
        public SslProtocols SslProtocol { get; set; } = SslProtocols.None;

        X509Certificate2 _Certificate;
        public X509Certificate2 Certificate
        {
            get
            {
                if (string.IsNullOrEmpty(Cert))
                {
                    return null;
                }
                return _Certificate ??= new System.Security.Cryptography.X509Certificates.X509Certificate2(Cert, Password);
            }
        }
    }

    public class ProxyConfig
    {
        public string Host { get; set; }
        public string Target { get; set; }
    }

    public enum ProxyType
    {
        None = 0,
        Http = 1,
        Socket = 2,       
        InternalProtocol = 3,
        //直接把数据转发到目标地址
        DirectSocket = 4
    }

}
