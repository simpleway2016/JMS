using JMS.HttpProxy.Dtos;
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
            if (HttpProxyProgram.Config.Current.Servers == null)
                throw new Exception("Servers 配置项为空");
            var proxyServerFactory = serviceProvider.GetService<ProxyServerFactory>();
                        

            foreach (var serverConfig in HttpProxyProgram.Config.Current.Servers)
            {
                ProxyServer server =  null;
                switch(serverConfig.Type)
                {
                    case ProxyType.Http:
                        server = new HttpServer();
                        break;
                    case ProxyType.DirectSocket:
                        server = new DirectSocketServer();
                        break;
                    case ProxyType.InternalProtocol:
                        server = new InternalProtocolServer();
                        break;
                    case ProxyType.Socket:
                        server = new SocketServer();
                        break;
                }
                if (server == null)
                    throw new Exception($"无法识别的类型：{serverConfig.Type}");
                server.Config = serverConfig;
                server.ServiceProvider = serviceProvider;
                server.Init();
                proxyServerFactory.Add(server);
            }

            return proxyServerFactory;
        }
    }
}
