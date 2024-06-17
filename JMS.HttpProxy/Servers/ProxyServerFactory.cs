using JMS.HttpProxy.Attributes;
using JMS.HttpProxy.Dtos;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Servers
{
    public class ProxyServerFactory
    {
        List<ProxyServer> _proxyServers = new List<ProxyServer>();
        public IEnumerable<ProxyServer> ProxyServers => _proxyServers;
       

        public void Add(ProxyServer server)
        {
            _proxyServers.Add(server);
        }
    }

    public static class ProxyServerFactoryExtensions
    {

        public static ProxyServerFactory UseProxyServerFactory(this IServiceProvider serviceProvider)
        {
            var proxyServerFactory = serviceProvider.GetService<ProxyServerFactory>();

            var types = typeof(ProxyServer).Assembly.GetTypes().Where(m => m.IsSubclassOf(typeof(ProxyServer))).ToArray();
            Dictionary<ProxyType, Type> proxyServerTypes = new Dictionary<ProxyType, Type>();
            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<ProxyTypeAttribute>();
                if (attr == null)
                    throw new InvalidOperationException($"{type.Name} miss ProxyTypeAttribute");
                proxyServerTypes[attr.ProxyType] = type;
            }

            foreach (var serverConfig in HttpProxyProgram.Config.Current.Servers)
            {
                var type = proxyServerTypes[serverConfig.Type];
                var server = (ProxyServer)Activator.CreateInstance(type);
                server.Config = serverConfig;
                server.ServiceProvider = serviceProvider;
                server.Init();
                proxyServerFactory.Add(server);
            }

            return proxyServerFactory;
        }
    }
}
