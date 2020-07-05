using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace JMS
{
    class TransactionIdBuilder
    {
        static int CurrentId = 0;
        public string Build()
        {
            return Interlocked.Increment(ref CurrentId).ToString();
        }
    }
}
