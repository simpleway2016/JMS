using JMS.HttpProxy.Applications.Http;
using JMS.HttpProxy.Applications.InternalProtocolSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Applications
{
    public class NetClientProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public NetClientProviderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public INetClientProvider GetNetClientProvider(string target)
        {
            if (target.StartsWith("http:") || target.StartsWith("https:"))
                return _serviceProvider.GetService<HttpNetClientProvider>();

            return _serviceProvider.GetService<SocketNetClientProvider>();
        }
    }
}
