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
        public static NetAddress[] GatewayAddresses;
        public static NetAddress ProxyAddresses;
      
        public MicroServiceClient():base(GatewayAddresses , ProxyAddresses)
        {

        }
    }
}
