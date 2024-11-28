using JMS.HttpProxy.Dtos;
using JMS.HttpProxy.Servers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy
{
    internal class ConfigWatch
    {
        private readonly ProxyServerFactory _proxyServerFactory;
        private readonly ILogger<ConfigWatch> _logger;
        bool _hasChanged = false;
        public ConfigWatch(ProxyServerFactory proxyServerFactory,ILogger<ConfigWatch> logger)
        {
            this._proxyServerFactory = proxyServerFactory;
            this._logger = logger;
        }

        public async Task Run()
        {
            foreach( var serverConfig in HttpProxyProgram.Config.Current.Servers)
            {
                OnAddServer(serverConfig);
            }
            HttpProxyProgram.Config.ValueChanged += Config_ValueChanged;
            while(true)
            {
                await Task.Delay(2000);
                if (_hasChanged)
                {
                    _hasChanged = false;
                    onChanged();
                }
            }
        }

        void OnAddServer(ServerConfig serverConfig)
        {
            _proxyServerFactory.Add(serverConfig);
        }

        void OnRemoveServer(ServerConfig serverConfig)
        {
            _proxyServerFactory.Remove(serverConfig);
        }

        void onChanged()
        {
            var newValues = HttpProxyProgram.Config.Current.Servers;
            var removeds = _proxyServerFactory.ProxyServers.Where(m => newValues.Any(n => n.Port == m.Key) == false).Select(m=>m.Value.Config);
            foreach (var removedServerConfig in removeds)
            {
                OnRemoveServer(removedServerConfig);
            }

            foreach (var serverConfig in newValues)
            {
                if (_proxyServerFactory.ProxyServers.TryGetValue(serverConfig.Port , out ProxyServer serverInstance))
                {
                    if (serverInstance.Config.Type != serverConfig.Type)
                    {
                        OnRemoveServer(serverInstance.Config);
                        OnAddServer(serverInstance.Config);
                    }
                    else
                    {
                        serverInstance.Config = serverConfig;
                    }
                }
                else
                {
                    OnAddServer(serverConfig);
                }
            }
        }

        private void Config_ValueChanged(object sender, Common.ValueChangedArg<Dtos.AppConfig> e)
        {
            var groupItemCounts = from m in e.NewValue.Servers
                         group m by m.Port into g
                         select g.Count();
            if (groupItemCounts.Any(m => m > 1))
            {
                _logger.LogError($"配置文件中有端口相同的配置");
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }
            _hasChanged = true;
           
        }
    }
}
