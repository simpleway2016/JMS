using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace JMS.Domains
{
    internal class ConnectionCounter : IConnectionCounter
    {
        int _ConnectionCount = 0;

        public int ConnectionCount => _ConnectionCount;

        public void OnDisconnect()
        {
            Interlocked.Decrement(ref _ConnectionCount);
        }

        public void OnConnect()
        {
            Interlocked.Increment(ref _ConnectionCount);
        }
    }
}
