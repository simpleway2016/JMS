using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace JMS
{
    public interface IConnectionCounter
    {
        /// <summary>
        /// 当前连接数
        /// </summary>
        int ConnectionCount { get; }
        ConcurrentDictionary<WebSocket, bool> WebSockets { get; }

        /// <summary>
        /// 记录有一个连接数增加
        /// </summary>
        void OnConnect();
        /// <summary>
        /// 记录减少一个连接数
        /// </summary>
        void OnDisconnect();
    }
}
