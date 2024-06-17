using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Applications
{
    public interface INetClientProvider
    {
        Task<NetClient> GetClientAsync(string target);
        void AddClientToPool(string target,NetClient netClient);
    }
}
