using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace JMS.WebApi
{

    public class WebApiConfig
    {
        public int Port { get; set; }
        public NetAddress[] Gateways { get; set; }
        public int MaxRequestLength { get; set; } = 5;
        public int InvokeTimeout { get; set; }
        public SSLConfig? SSL { get; set; }
        public string[] ProxyIps { get; set; }
        public RequesttimeConfig RequestTime { get; set; }
        public DocConfig Http { get; set; }
    }

    public class SSLConfig
    {
        public string Cert { get; set; }
        public string Password { get; set; }
        public SslProtocols? SslProtocol { get; set; }
        public string[] AcceptCertHash { get; set; }
    }

    public class RequesttimeConfig
    {
        public int Limit { get; set; }
        public int LockMinutes { get; set; }
    }

    public class DocConfig
    {
        public bool SupportJmsDoc { get; set; }
        public bool AllServiceInDoc { get; set; }
    }


}
