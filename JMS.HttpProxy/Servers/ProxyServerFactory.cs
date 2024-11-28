using JMS.HttpProxy.Attributes;
using JMS.HttpProxy.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Servers
{
    public class ProxyServerFactory
    {
        ConcurrentDictionary<int, ProxyServer> _proxyServers = new ConcurrentDictionary<int, ProxyServer>();
        public ConcurrentDictionary<int, ProxyServer> ProxyServers => _proxyServers;
        Dictionary<ProxyType, Type> _proxyServerTypes = new Dictionary<ProxyType, Type>();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProxyServerFactory> _logger;

        public ProxyServerFactory(IServiceProvider serviceProvider, ILogger<ProxyServerFactory> logger)
        {
            this._serviceProvider = serviceProvider;
            this._logger = logger;
            var types = typeof(ProxyServer).Assembly.GetTypes().Where(m => m.IsSubclassOf(typeof(ProxyServer))).ToArray();

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<ProxyTypeAttribute>();
                if (attr == null)
                    throw new InvalidOperationException($"{type.Name} miss ProxyTypeAttribute");
                _proxyServerTypes[attr.ProxyType] = type;
            }


        }

        public void Add(ServerConfig serverConfig)
        {
            var type = _proxyServerTypes[serverConfig.Type];
            var server = (ProxyServer)Activator.CreateInstance(type);
            server.Config = serverConfig;
            server.ServiceProvider = _serviceProvider;
            server.Init();
            new Thread(() => server.Run()).Start();

            _proxyServers[serverConfig.Port] = server;
        }

        public void Remove(ServerConfig serverConfig)
        {
            if (_proxyServers.TryRemove(serverConfig.Port, out ProxyServer server))
            {
                _logger.LogWarning($"Removed port:{server.Config.Port}");
                server.Dispose();
            }
        }
    }

}
