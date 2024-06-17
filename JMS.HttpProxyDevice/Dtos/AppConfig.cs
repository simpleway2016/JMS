using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxyDevice.Dtos
{

    public class AppConfig
    {
        public NetAddress ProxyServer { get; set; }
        public bool LogDetails { get; set; }
        public DeviceConfig Device {  get; set; }
    }


    public class DeviceConfig
    {
        public string Name { get; set; }
        public string Password { get; set; }
        public int ConnectionCount { get; set; }
    }

}
