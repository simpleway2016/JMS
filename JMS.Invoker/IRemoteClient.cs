﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    interface IRemoteClient
    {
        bool SupportTransaction { get; }
        string TransactionId { get; }
        NetAddress GatewayAddress { get;  }
        NetAddress ProxyAddress { get; }
        int Timeout { get; set; }
        X509Certificate2 ServiceClientCertificate { get; set; }
        Dictionary<string, string> GetCommandHeader();
        void AddConnect(IInvokeConnect connect);
        void AddTask(IInvokeConnect connect , int invokingId, Task task);
    }
}
