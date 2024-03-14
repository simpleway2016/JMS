using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.TransactionReporters
{
    internal interface ITransactionReporter
    {
        void ReportTransactionSuccess(RemoteClient remoteClient, string tranid);
        void ReportTransactionCompleted(RemoteClient remoteClient, string tranid);
    }
}
