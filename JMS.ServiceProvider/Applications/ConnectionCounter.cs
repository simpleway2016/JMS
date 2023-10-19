using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace JMS
{
    internal class ConnectionCounter : IConnectionCounter
    {
        int _ConnectionCount = 0;
        ConcurrentDictionary<WebSocket, bool> _webSocketList = new ConcurrentDictionary<WebSocket, bool>();
        public ConcurrentDictionary<WebSocket, bool> WebSockets => _webSocketList;
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
