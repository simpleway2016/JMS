using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace JMS.Common.Net
{
    public class TcpServer
    {
        int _port;
        public int Port => _port;
        TcpListener _tcpListener;
        TcpListener _tcpListenerV6;
        public event EventHandler<Socket> Connected;
        public TcpServer(int port)
        {
            this._port = port;
            _tcpListener = new TcpListener(IPAddress.Any, port);
            _tcpListenerV6 = new TcpListener(IPAddress.IPv6Any, port);
        }

        public void Stop()
        {
            _tcpListener?.Stop();
            _tcpListenerV6?.Stop();

            _tcpListener = null;
            _tcpListenerV6 = null;
        }

        public void Run()
        {
            new Thread(() => { runListener(_tcpListenerV6); }).Start();
            this.runListener(_tcpListener);
        }

        void runListener(TcpListener listener)
        {
            try
            {
                listener.Start();
                while (true)
                {
                    var socket = listener.AcceptSocket();
                    if (this.Connected != null)
                    {
                        this.Connected(this, socket);
                    }
                }
            }
            catch
            {

            }
        }
    }
}
