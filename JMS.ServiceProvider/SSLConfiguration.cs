using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace JMS
{
    public class SSLConfiguration
    {
        /// <summary>
        /// 与网关通讯的证书
        /// </summary>
        public X509Certificate2 GatewayClientCertificate { get; set; }
        /// <summary>
        /// 微服务证书
        /// </summary>
        public X509Certificate2 ServerCertificate { get; set; }
        /// <summary>
        /// 哪些客户端证书被信任，空表示信任所有证书
        /// </summary>
        public string[] AcceptClientCertHash { get; set; }
    }

    public static class SSLConfigurationExtension
    {
        public static MicroServiceHost UseSSL(this MicroServiceHost host , Action< SSLConfiguration> config )
        {
            if(config != null)
            {
                SSLConfiguration configuration = new SSLConfiguration();
                host._services.AddSingleton<SSLConfiguration>(configuration);
                config(configuration);
            }
            return host;
        }
    }
}
