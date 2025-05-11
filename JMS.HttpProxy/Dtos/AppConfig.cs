using JMS.ServerCore;
using Microsoft.Extensions.Configuration;
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
        public string[] ProxyIps { get; set; }
        public bool LogDetails { get; set; }
        public DeviceConfig[] Devices { get; set; }
        public ServerConfig[] Servers { get; set; }
        public Dictionary<string, string> ContentTypes { get; set; }
    }

    public class DeviceConfig
    {
        public string Name { get; set; }
        public string Password { get; set; }
    }

    public class ServerConfig
    {
        public ProxyType Type { get; set; }
        public SslConfig SSL { get; set; }
        public int Port { get; set; }
        public ProxyConfig[] Proxies { get; set; }
    }

    public enum DomainProvider
    {
        AlibabaCloud = 1
    }

    public class AcmeConfig
    {
        public string Domain { get; set; }
        public string Password { get; set; } = "123456";
        public DomainProvider DomainProvider { get; set; }
        public string AccessKeyId { get; set; }
        public string AccessKeySecret { get; set; }
        public int PreDays { get; set; } = 5;
    }

    public class SslConfig
    {
        public string Cert { get; set; }
        public string Password { get; set; }
        public string PrivateKeyPath { get; set; }

        public AcmeConfig Acme { get; set; }

        public SslProtocols SslProtocol { get; set; } = SslProtocols.None;

        X509Certificate2 _Certificate;
        [System.Text.Json.Serialization.JsonIgnore]
        public X509Certificate2 Certificate
        {
            get
            {
                if (string.IsNullOrEmpty(Cert))
                {
                    return null;
                }
                if (!string.IsNullOrEmpty(PrivateKeyPath))
                {
                    return _Certificate ??= CertificateHelper.LoadCertificate(Cert, PrivateKeyPath);
                }
                else
                {
                    return _Certificate ??= X509CertificateLoader.LoadPkcs12FromFile(Cert, Password);
                }
            }

        }
    }

    public class ProxyConfig
    {
        public string Host { get; set; }
        public string Target { get; set; }
        /// <summary>
        /// 静态文件目录
        /// </summary>
        public string RootPath { get; set; }
        /// <summary>
        /// 默认页面
        /// </summary>
        public string DefaultPage { get; set; }
        /// <summary>
        /// 是否自动修改请求的Host头
        /// </summary>
        public bool ChangeHostHeader { get; set; }
        public string AccessControlAllowOrigin { get; set; }
    }



    public enum ProxyType
    {
        None = 0,
        Http = 1,
        //把数据转发到InternalProtocol客户端
        InternalProtocolSocket = 2,
        //接受HttpProxyDevice的注册
        InternalProtocol = 3,
        //直接把数据转发到目标地址
        DirectSocket = 4,
        /// <summary>
        /// 静态文件
        /// </summary>
        StaticFiles = 5
    }

}
