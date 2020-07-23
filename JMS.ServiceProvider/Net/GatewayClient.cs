
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace JMS.Net
{
    class GatewayClient : CertClient
    {
        public GatewayClient(NetAddress addr,SSLConfiguration sSLConfiguration):base(addr , sSLConfiguration != null ? sSLConfiguration.GatewayClientCertificate : null )
        {
        }
    }
}
