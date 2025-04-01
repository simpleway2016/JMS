using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.ServerCore
{
    public class MulitTcpListener
    {
        int _port;
        private readonly ILogger _logger;

        public int Port => _port;
        TcpListener _tcpListener;
        TcpListener _tcpListenerV6;
        public event EventHandler<Socket> Connected;
        public event EventHandler<Exception> OnError;
        bool _stopped = true;
        public MulitTcpListener(int port , ILogger logger)
        {
            this._port = port;
            _logger = logger;
        }

        public void Stop()
        {
            _stopped = true;
            _tcpListener?.Stop();
            _tcpListenerV6?.Stop();

            _tcpListener = null;
            _tcpListenerV6 = null;
        }

        public async Task RunAsync()
        {
            if (_stopped == false)
                return;

            _stopped = false;
            _tcpListener = new TcpListener(IPAddress.Any, _port);
            _tcpListenerV6 = new TcpListener(IPAddress.IPv6Any, _port);

            _logger?.LogInformation($"Service is starting");

            _ = runListenerAsync(_tcpListenerV6);

            await this.runListenerAsync(_tcpListener);
        }

        async Task runListenerAsync(TcpListener listener)
        {
            try
            {
                bool isV6 = false;
                if(((IPEndPoint)listener.LocalEndpoint).Address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    isV6 = true;
                }

               
                listener.Start();

                if (isV6)
                {
                    _logger?.LogInformation($"Listening on port {_port} of IPv6");
                }
                else
                {
                    _logger?.LogInformation($"Listening on port {_port}");
                }
                while (true)
                {
                    var socket = await listener.AcceptSocketAsync();
                    if (this.Connected != null)
                    {
                        this.Connected(this, socket);
                    }
                }
            }
            catch(Exception ex)
            {               
                if (!_stopped)
                {
                    if (OnError == null)
                    {
                        _logger?.LogError(ex, "");
                    }
                    else
                    {
                        OnError(this, ex);
                    }
                }
            }
        }
    }
}
