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
        /// <summary>
        /// 询问网关是否是主网关
        /// </summary>
        FindMaster = 8,
        UploadLockKeys = 9,
        HealthyCheck =10,
        /// <summary>
        /// 监测共享文件的变化
        /// </summary>
        ListenFileChange = 11,
        GetShareFile = 12,
        SetAllServices = 13,
        ServiceNameListChanged = 14
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
