using JMS.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace JMS.Proxy
{
    class Connect : IDisposable
    {
        NetClient _client;
        NetClient _targetClient;
        Proxy _proxy;
        ILogger<Connect> _logger;
        public Connect(NetClient client, Proxy proxy)
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

        public async Task Start()
        {
            var target = await _client.ReadServiceObjectAsync<NetAddress>();
            if (target == null)
            {
                _client.Write(Encoding.UTF8.GetBytes("ok"));
                _client.Dispose();
                return;
            }
            _logger?.LogTrace("收到转发{0} {1}", target.Address, target.Port);

            _targetClient = new CertClient(_proxy.ClientCert);
            await _targetClient.ConnectAsync(target);
            _targetClient.ReadTimeout = 0;

            readWrite(_targetClient, _client);

            await readWrite(_client, _targetClient);
           
        }

        async Task readWrite(NetClient readClient,NetClient writeClient)
        {
            byte[] buffer = new byte[4096];
            while (true)
            {
                int flag = await readClient.ReadIntAsync();
                var len = flag >> 2;
                if (buffer.Length < len)
                    buffer = new byte[len];

                await readClient.ReadDataAsync(buffer, 0, len);

                writeClient.Write(flag);
                writeClient.InnerStream.Write(buffer, 0, len);
            }
        }

    }
}
