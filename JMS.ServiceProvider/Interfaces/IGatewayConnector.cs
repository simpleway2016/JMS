
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface IGatewayConnector
    {
        NetClient CreateClient(NetAddress addr);
        void DisconnectGateway();
        void ConnectAsync();
        void OnServiceNameListChanged();
    }
}
