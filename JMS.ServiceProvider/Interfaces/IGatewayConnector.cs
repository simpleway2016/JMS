
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface IGatewayConnector
    {
        Action OnConnectCompleted { get; set; }
        NetClient CreateClient(NetAddress addr);
        void DisconnectGateway();
        void ConnectAsync();
        void OnServiceNameListChanged();
    }
}
