using JMS.HttpProxy.InternalProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Applications.Sockets
{
    public class SocketNetClientProvider : INetClientProvider
    {
        private readonly InternalConnectionProvider _connectionProvider;

        public SocketNetClientProvider(InternalConnectionProvider connectionProvider)
        {
            _connectionProvider = connectionProvider;
        }

        public async Task<NetClient> GetClientAsync(string target)
        {
            var index = target.IndexOf(":");
            var port = 80;
            if(index > 0)
            {
                port = int.Parse( target.Substring(index + 1).Trim());
            }

            var client = await NetClientPool.CreateClientByKeyAsync(target);
            if(client == null)
            {
                client = await _connectionProvider.GetConnectionAsync(target);
            }

            client.Write(port);

            return client;
        }

        public void AddClientToPool(string target , NetClient netClient)
        {
            NetClientPool.AddClientToPoolByKey( netClient , target);
        }
    }
}
