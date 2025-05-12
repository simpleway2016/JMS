using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace JMS
{
    public class SSLConfiguration
    {
        public SslServerAuthenticationOptions SslServerAuthenticationOptions { get; private set; }

        X509Certificate2 _ServerCertificate;
        /// <summary>
        /// 微服务证书
        /// </summary>
        public X509Certificate2 ServerCertificate {
            get => _ServerCertificate;

            set
            {
                if (_ServerCertificate != value)
                {
                    var old = _ServerCertificate;
                    _ServerCertificate = value;
                    old?.Dispose();

                    if (this.SslServerAuthenticationOptions != null)
                    {
                        foreach (var cert in this.SslServerAuthenticationOptions.ServerCertificateContext.IntermediateCertificates)
                        {
                            cert.Dispose();
                        }
                    }

                    if (_ServerCertificate != null)
                    {
                        this.SslServerAuthenticationOptions = new SslServerAuthenticationOptions()
                        {
                            ServerCertificateContext = SslStreamCertificateContext.Create(_ServerCertificate, null),
                            RemoteCertificateValidationCallback = RemoteCertificateValidationCallback,
                            ClientCertificateRequired = false,
                            EnabledSslProtocols =  SslProtocols.None
                        };
                    }
                    else
                    {
                        this.SslServerAuthenticationOptions = null;
                    }

                }
            }
        }
     
        /// <summary>
        /// 哪些客户端证书被信任，空表示信任所有证书
        /// </summary>
        public string[] AcceptClientCertHash { get; set; }

        internal bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (AcceptClientCertHash != null && AcceptClientCertHash.Length > 0
                      && AcceptClientCertHash.Contains(certificate?.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }
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
