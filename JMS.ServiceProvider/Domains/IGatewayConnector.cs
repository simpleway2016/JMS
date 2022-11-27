
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public interface IGatewayConnector
    {
        event EventHandler ConnectCompleted;
        NetClient CreateClient(NetAddress addr);
        void DisconnectGateway();
        void ConnectAsync();
        void OnServiceInfoChanged();
        /// <summary>
        /// 检查事务是否已成功
        /// </summary>
        /// <param name="tranid"></param>
        /// <returns></returns>
        Task<bool> CheckTransactionAsync(string tranid);

        bool CheckTransaction(string tranid);
    }
}
