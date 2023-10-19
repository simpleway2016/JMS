using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public interface IWebApiEnvironment
    {
        string AppSettingPath { get; }
        int Port { get; set; }
        NetAddress[] GatewayAddresses { get; set; }
        X509Certificate2 ServerCert { get;  set; }
        string[] AcceptCertHash { get; set; }
    }

    class DefaultWebApiEnvironment : IWebApiEnvironment
    {
        public string AppSettingPath { get; }
        public int Port { get; set; }
        public NetAddress[] GatewayAddresses { get; set; }
        public X509Certificate2 ServerCert { get; set; }
        public string[] AcceptCertHash { get; set; }

        public DefaultWebApiEnvironment(string appSettingPath,int port)
        {
            AppSettingPath = appSettingPath;
            Port = port;
        }
    }
}
