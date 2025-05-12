using Org.BouncyCastle.Crypto.Tls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace JMS
{
    internal interface ISslConfiguration
    {
        string[] AcceptCertHash { get; }
        X509Certificate2 ServerCert { get; }
        SslServerAuthenticationOptions SslServerAuthenticationOptions { get; }
    }

    class DefaultSslConfiguration : ISslConfiguration
    {
        public X509Certificate2 ServerCert { get;}
        public string[] AcceptCertHash { get; }

        public SslServerAuthenticationOptions SslServerAuthenticationOptions { get; }

        public DefaultSslConfiguration(X509Certificate2 serverCert,SslProtocols sslProtocol, string[] acceptCertHash)
        {
            ServerCert = serverCert;
            AcceptCertHash = acceptCertHash;

            if(serverCert != null)
            {
                this.SslServerAuthenticationOptions = new SslServerAuthenticationOptions()
                {
                    ServerCertificateContext = SslStreamCertificateContext.Create(ServerCert, null),
                    RemoteCertificateValidationCallback = RemoteCertificateValidationCallback,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = sslProtocol
                };
            }
           
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (AcceptCertHash != null && AcceptCertHash.Length > 0 && AcceptCertHash.Contains(certificate?.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }
    }
}
