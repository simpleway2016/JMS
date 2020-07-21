using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Dtos
{
    public enum InvokeType
    {
        Invoke = 0,
        CommitTranaction = 1,
        RollbackTranaction = 2,
        /// <summary>
        /// 获取访问服务的c#代码类
        /// </summary>
        GenerateInvokeCode = 3,
        HealthyCheck=4,
        GetAllLockedKeys = 5,
        /// <summary>
        /// 强制unlock key
        /// </summary>
        UnlockKeyAnyway = 6
    }
    public class InvokeCommand
    {
        public InvokeType Type = InvokeType.Invoke;
        public IDictionary<string, string> Header;
        public string Service;
        public string Method;
        public string[] Parameters;
    }

    public class InvokeResult
    {
        public bool Success;
        public bool SupportTransaction;
        public object Data;
        public string Error;
    }
    public class InvokeResult<T>
    {
        public bool Success;
        public bool SupportTransaction;
        public T Data;
        public string Error;
    }
}
