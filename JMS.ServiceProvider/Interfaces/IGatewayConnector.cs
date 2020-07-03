using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface IGatewayConnector
    {
        int ServiceId { get; }
        void DisconnectGateway();
        void ConnectAsync();
    }
}
