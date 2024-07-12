using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    internal interface ISslConfiguration
    {
        string[] AcceptCertHash { get; }
        X509Certificate2 ServerCert { get; }
        public SslProtocols SslProtocol { get; }
    }

    class DefaultSslConfiguration : ISslConfiguration
    {
        public X509Certificate2 ServerCert { get;}
        public SslProtocols SslProtocol { get; }
        public string[] AcceptCertHash { get; }

        public DefaultSslConfiguration(X509Certificate2 serverCert,SslProtocols sslProtocol, string[] acceptCertHash)
        {
            ServerCert = serverCert;
            AcceptCertHash = acceptCertHash;
            SslProtocol = sslProtocol;
        }
    }
}
