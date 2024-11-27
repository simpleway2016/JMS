using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Applications.Http
{
    public class HttpNetClientProvider : INetClientProvider
    {
        public void AddClientToPool(string target,NetClient netClient)
        {
            NetClientPool.AddClientToPool(netClient);
        }

        public Task<NetClient> GetClientAsync(string target)
        {
            var targetUri = new Uri(target);
            return NetClientPool.CreateClientAsync(null, new NetAddress(targetUri.Host, targetUri.Port)
            {
                UseSsl = string.Equals(targetUri.Scheme, "https", StringComparison.OrdinalIgnoreCase) || string.Equals(targetUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase),
                CertDomain = targetUri.Host
            });
        }
    }
}
