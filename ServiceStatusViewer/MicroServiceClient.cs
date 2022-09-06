using JMS;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceStatusViewer
{
    public class MicroServiceClient : RemoteClient
    {
        static NetAddress[] GatewayAddresses;
        static NetAddress ProxyAddresses;
        static IConfiguration Configuration;
        static MicroServiceClient()
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile(Program.SettingFileName, optional: true, reloadOnChange: true);
            Configuration = builder.Build();
            ConfigurationChangeCallback(null);
        }

        static void ConfigurationChangeCallback(object p)
        {
            Configuration.GetReloadToken().RegisterChangeCallback(ConfigurationChangeCallback, null);

            GatewayAddresses = Configuration.GetSection("Gateways").Get<NetAddress[]>();
            ProxyAddresses = Configuration.GetSection("Proxy").Get<NetAddress>();
        }
        public MicroServiceClient():base(GatewayAddresses , ProxyAddresses)
        {

        }
    }
}
