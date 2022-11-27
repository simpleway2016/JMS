using JMS.Common;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public class CertClient : NetClient
    {
        X509Certificate2 _cert;

        public CertClient(X509Certificate2 cert)
        {
            this._cert = cert;

        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public override void Connect(string address, int port)
        {
            base.Connect(address, port);
            if(_cert != null)
            {
                SslStream sslStream = new SslStream(this.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), null);
                X509CertificateCollection certs = new X509CertificateCollection();
                certs.Add(_cert);
                sslStream.AuthenticateAsClient("SslSocket", certs, NetClient.SSLProtocols, false);
                this.InnerStream = sslStream;
            }
        }

        public override async Task ConnectAsync(string address, int port)
        {
            await base.ConnectAsync(address, port);
            if (_cert != null)
            {
                SslStream sslStream = new SslStream(this.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), null);
                X509CertificateCollection certs = new X509CertificateCollection();
                certs.Add(_cert);
                await sslStream.AuthenticateAsClientAsync("SslSocket", certs, NetClient.SSLProtocols, false);
                this.InnerStream = sslStream;
            }
        }
    }
}
