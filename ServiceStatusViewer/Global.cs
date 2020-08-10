using JMS;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceStatusViewer
{
    static class Global
    {
        public static IConfiguration Configuration { get; set; }

        public static NetAddress[] GatewayAddresses
        {
            get;
            set;
        }
    }
}
