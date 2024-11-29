using JMS.HttpProxy.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Servers
{
    public abstract class ProxyServer: IDisposable
    {
        public abstract void Init();
        public abstract void Run();

        public abstract void Dispose();

        public ServerConfig Config { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
    }
}
