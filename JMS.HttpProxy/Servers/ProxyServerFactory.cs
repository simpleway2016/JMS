using JMS.HttpProxy.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

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

            _proxyServerTypes[ProxyType.DirectSocket] = typeof(DirectSocketServer);
            _proxyServerTypes[ProxyType.Http] = typeof(HttpServer);
            _proxyServerTypes[ProxyType.InternalProtocol] = typeof(InternalProtocolServer);
            _proxyServerTypes[ProxyType.Socket] = typeof(SocketServer);

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
