using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    interface IRemoteClient
    {

        string TransactionId { get; set; }
        NetAddress GatewayAddress { get;  }
        NetAddress ProxyAddress { get; }
        int Timeout { get; set; }
        X509Certificate2 GatewayClientCertificate { get; set; }
        X509Certificate2 ServiceClientCertificate { get; set; }
        Dictionary<string, string> GetCommandHeader();
        void AddConnect(InvokeConnect connect);
        void AddTask(Task task);
    }
}
