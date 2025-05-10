using JMS.HttpProxy.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Servers
{
    public abstract class ProxyServer: IDisposable
    {
        public abstract void Init();
        public abstract Task RunAsync();

        public abstract void Dispose();
        public virtual X509Certificate2 Certificate { get; set; }
        public virtual ServerConfig Config { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
       
    }
}
