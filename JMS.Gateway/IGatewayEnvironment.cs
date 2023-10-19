using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public interface IGatewayEnvironment
    {
        string AppSettingPath { get; }
        int Port { get; set; }
    }

    class DefaultGatewayEnvironment : IGatewayEnvironment
    {
        public string AppSettingPath { get; }
        public int Port { get; set; }

        public DefaultGatewayEnvironment(string appSettingPath, int port)
        {
            AppSettingPath = appSettingPath;
            Port = port;    
        }
    }
}
