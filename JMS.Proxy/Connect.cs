using JMS.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace JMS.Proxy
{
    class Connect:IDisposable
    {
        NetClient _client;
        NetClient _targetClient;
        Proxy _proxy;
        ILogger<Connect> _logger;
        public Connect(NetClient client,Proxy proxy)
        {
            _client = client;
            _proxy = proxy;
            _logger = proxy.ServiceProvider.GetService<ILogger<Connect>>();
            _client.ReadTimeout = 0;
        }

        public void Dispose()
        {
            _targetClient?.Dispose();
            _client.Dispose();
        }

        public void Start()
        {
            var target = _client.ReadServiceObject<NetAddress>();
            if(target == null)
            {
                _client.Write(Encoding.UTF8.GetBytes("ok"));
                _client.Dispose();
                return;
            }
            _logger?.LogDebug("收到转发{0} {1}", target.Address, target.Port);

            _targetClient = new CertClient(_proxy.ClientCert);
            _targetClient.Connect(target);
            _targetClient.ReadTimeout = 0;

            new Thread(readTarget).Start();

            byte[] buffer = new byte[4096];
            while (true)
            {
                int flag = _client.ReadInt();
                var len = flag >> 2;
                if (buffer.Length < len)
                    buffer = new byte[len];

                _client.ReadData(buffer , 0 , len);

                _targetClient.Write(flag);
                _targetClient.InnerStream.Write(buffer, 0, len);
            }
        }

        void readTarget()
        {
            try
            {
                byte[] buffer = new byte[4096];
                while (true)
                {
                    int flag = _targetClient.ReadInt();
                    var len = flag >> 2;
                    if (buffer.Length < len)
                        buffer = new byte[len];

                    _targetClient.ReadData(buffer, 0, len);

                    _client.Write(flag);
                    _client.InnerStream.Write(buffer, 0, len);
                }
            }
            catch (System.IO.IOException ex)
            {
                if (ex.InnerException is SocketException)
                    return;
                throw ex;
            }
            catch (SocketException)
            {

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
            }            
        }
    }
}
