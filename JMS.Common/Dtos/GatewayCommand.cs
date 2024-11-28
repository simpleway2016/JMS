using JMS.Common.Collections;
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
        ServiceNameListChanged = 14,
        CheckSupportSingletonService = 15,
        HttpRequest = 16,
        ReportTransactionStatus = 17,
        GetTransactionStatus = 18,
        RemoveTransactionStatus = 19,
        BeGatewayMaster = 20,
        AddLockKey = 21,
        RemoveLockKey = 22,
        RemoteClientConnection = 23,
        SetApiDocumentButton = 24,
        RemoveApiDocumentButton = 25,
        GetApiDocumentButtons = 26
    }
    public class GatewayCommand
    {
        public int Type;
        public IgnoreCaseDictionary Header = new IgnoreCaseDictionary();
        public string Content;
        public bool IsHttp;
    }

    public class GetServiceProviderRequest
    {
        public string ServiceName;
        public IgnoreCaseDictionary Header = new IgnoreCaseDictionary();
        public string ClientAddress;
        public bool IsGatewayProxy;
    }

}
