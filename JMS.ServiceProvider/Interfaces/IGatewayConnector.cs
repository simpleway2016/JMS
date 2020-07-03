using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface IGatewayConnector
    {
        void DisconnectGateway();
        void ConnectAsync();
    }
}
