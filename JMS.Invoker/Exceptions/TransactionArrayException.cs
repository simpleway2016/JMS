using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    /// <summary>
    /// 提交事务过程中发送错误,通过AllExceptions获取哪个服务发生错误
    /// </summary>
    public class TransactionArrayException : Exception
    {
        public List<TransactionException> AllExceptions { get; }
        public TransactionArrayException(List<TransactionException> allExceptions, string message) : base(message)
        {
            AllExceptions = allExceptions;
        }
    }
}

