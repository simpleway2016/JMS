using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    public interface ITransactionRecorder
    {
        void Record(TransactionDelegate tranDelegate);
    }
}
