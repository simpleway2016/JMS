﻿using JMS.Common;
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
        public static RemoteCertificateValidationCallback RemoteCertificateValidationCallback = null;
        internal static RemoteCertificateValidationCallback NOCHECK = new RemoteCertificateValidationCallback(remoteCertificateValidationCallback);

        public CertClient()
        {
        }

        static bool remoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public override void Connect(NetAddress addr)
        {
            base.Connect(addr);
            this.AfterConnect();
            if (addr.UseSsl)
            {
                SslStream sslStream = new SslStream(this.InnerStream, false, CertClient.RemoteCertificateValidationCallback ?? NOCHECK, null);
                if (addr.Certificate != null)
                {
                    X509CertificateCollection certs = new X509CertificateCollection();
                    certs.Add(addr.Certificate);
                    sslStream.AuthenticateAsClient(addr.CertDomain??"", certs, NetClient.SSLProtocols, false);
                }
                else
                {
                    sslStream.AuthenticateAsClient(addr.CertDomain ?? "");
                }
                this.InnerStream = sslStream;
            }
        }

        public override async Task ConnectAsync(NetAddress addr)
        {
            await base.ConnectAsync(addr);
            this.AfterConnect();
            if (addr.UseSsl)
            {
                SslStream sslStream = new SslStream(this.InnerStream, false, CertClient.RemoteCertificateValidationCallback ?? NOCHECK, null);
                if (addr.Certificate != null)
                {
                    X509CertificateCollection certs = new X509CertificateCollection();
                    certs.Add(addr.Certificate);
                    await sslStream.AuthenticateAsClientAsync(addr.CertDomain ?? "", certs, NetClient.SSLProtocols, false);
                }
                else
                {
                    await sslStream.AuthenticateAsClientAsync(addr.CertDomain ?? "");
                }
                this.InnerStream = sslStream;
            }
        }


        protected virtual void AfterConnect()
        {

        }
    }
}
