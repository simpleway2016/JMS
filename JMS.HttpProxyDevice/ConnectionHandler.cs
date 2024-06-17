using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxyDevice
{
    public class ConnectionHandler
    {

        public async Task Handle(NetClient client, int targetPort)
        {
            using var proxyClient = await NetClientPool.CreateClientAsync(null, new NetAddress("127.0.0.1", targetPort));

            client.ReadTimeout = 0;
            proxyClient.ReadTimeout = 0;

            proxyClient.ReadAndSendForLoop(client);

            await client.ReadAndSendForLoop(proxyClient);
        }
    }
}
