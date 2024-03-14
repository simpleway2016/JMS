using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class RemoteException:Exception
    {
        public string TransactionId { get; }
        public int? StatusCode { get; }
        public RemoteException(string tranid,int? statusCode, string message):base(message)
        {
            this.StatusCode = statusCode;
            this.TransactionId = tranid;
        }
    }
}
