using JMS.Common;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace JMS
{
    public class CertClient : NetClient
    {
        public CertClient(NetAddress addr, X509Certificate2 cert):base(addr)
        {
            loadCert(cert);
        }
        public CertClient(string ip,int port, X509Certificate2 cert) : base(ip,port)
        {
            loadCert(cert);
        }

        void loadCert(X509Certificate2 cert)
        {
            if (cert != null)
            {
                SslStream sslStream = new SslStream(this.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), null);
                X509CertificateCollection certs = new X509CertificateCollection();
                certs.Add(cert);
                sslStream.AuthenticateAsClient("SslSocket", certs, NetClient.SSLProtocols, false);
                this.InnerStream = sslStream;
            }
        }
        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
