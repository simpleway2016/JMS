
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace JMS.Net
{
    class GatewayClient : NetClient
    {
        public GatewayClient(NetAddress addr,SSLConfiguration sSLConfiguration):base(addr)
        {
            if (sSLConfiguration != null && sSLConfiguration.GatewayClientCertificate != null)
            {
                SslStream sslStream = new SslStream(this.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), null);
                X509CertificateCollection certs = new X509CertificateCollection();
                certs.Add(sSLConfiguration.GatewayClientCertificate);
                sslStream.AuthenticateAsClient("SslSocket", certs, System.Security.Authentication.SslProtocols.Tls, true);
                this.InnerStream = sslStream;
            }
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
