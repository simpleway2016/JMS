using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class RemoteException:Exception
    {
        public string TransactionId { get; }
        public RemoteException(string tranid, string message):base(message)
        {
            this.TransactionId = tranid;
        }
    }
}
