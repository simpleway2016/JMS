using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    /// <summary>
    /// 提交事务过程中发送错误
    /// </summary>
    public class TransactionCommitException : Exception
    {
        public RegisterServiceLocation ServiceLocation { get; }
        public TransactionCommitException(RegisterServiceLocation serviceLocation, string message):base(message)
        {
            ServiceLocation = serviceLocation;
        }
    }
}
