
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
        /// <summary>
        /// 检查事务是否已成功
        /// </summary>
        /// <param name="tranid"></param>
        /// <returns></returns>
        bool CheckTransaction(string tranid);
    }
}
