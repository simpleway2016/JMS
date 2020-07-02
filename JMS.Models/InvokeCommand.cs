using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public enum InvokeType
    {
        None = 0,
        CommitTranaction = 1,
        RollbackTranaction = 2
    }
    public class InvokeCommand
    {
        public InvokeType Type = InvokeType.None;
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
