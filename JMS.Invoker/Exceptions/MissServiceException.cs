using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class RemoteException:Exception
    {
        public string TransactionId { get; }
        public int? StatusCode { get; }
        public string Error { get; set; }
        public RemoteException(string tranid,int? statusCode,string error, string message):base(message)
        {
            this.StatusCode = statusCode;
            this.TransactionId = tranid;
            this.Error = error;
        }
    }
}
