using JMS;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceStatusViewer
{
    public class MicroServiceClient : JMSClient
    {
        static NetAddress[] GatewayAddresses;
        static IConfiguration Configuration;
        static MicroServiceClient()
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            Configuration = builder.Build();
            ConfigurationChangeCallback(null);
        }

        static void ConfigurationChangeCallback(object p)
        {
            Configuration.GetReloadToken().RegisterChangeCallback(ConfigurationChangeCallback, null);

            GatewayAddresses = Configuration.GetSection("Gateways").Get<NetAddress[]>();
        }
        public MicroServiceClient():base(GatewayAddresses)
        {

        }
    }
}
