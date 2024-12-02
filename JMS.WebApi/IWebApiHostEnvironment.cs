using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public interface IWebApiHostEnvironment
    {
        string AppSettingPath { get; }
        int Port { get; set; }
        int MaxRequestLength { get; set; }
        NetAddress[] GatewayAddresses { get; set; }
        X509Certificate2 ServerCert { get;  set; }
        SslProtocols SslProtocol { get; set; }
        string[] AcceptCertHash { get; set; }
    }

    class DefaultWebApiHostEnvironment : IWebApiHostEnvironment
    {
        public string AppSettingPath { get; }
        public int Port { get; set; }
        public int MaxRequestLength { get; set; }
        public NetAddress[] GatewayAddresses { get; set; }
        public X509Certificate2 ServerCert { get; set; }
        public SslProtocols SslProtocol { get; set; } = SslProtocols.None;
        public string[] AcceptCertHash { get; set; }

        public DefaultWebApiHostEnvironment(string appSettingPath,int port)
        {
            AppSettingPath = appSettingPath;
            Port = port;
        }
    }
}
