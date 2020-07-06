using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JMS.Dtos
{
    public enum CommandType
    {
        RegisterSerivce = 1,
        GetServiceProvider = 2,
        GetAllServiceProviders = 3,
        ReportClientConnectQuantity = 4,
        LockKey = 5,
        ApplyToBeMaster = 6,
        UnRegisterSerivce = 7,
        FindMaster = 8
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
