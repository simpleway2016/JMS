using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JMS.Dtos
{
    public enum CommandType
    {
        Register = 1,
        GetServiceProvider = 2,
        GetAllServiceProviders = 3,
        ReportClientConnectQuantity = 4,
        LockKey = 5
    }
    public class GatewayCommand
    {
        public CommandType Type;
        public IDictionary<string, string> Header;
        public string Content;
    }

    public class GetServiceProviderRequest
    {
        public string ServiceName;
        public IDictionary<string, string> Header;
        public string ClientAddress;
    }
}
