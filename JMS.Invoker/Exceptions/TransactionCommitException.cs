using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    /// <summary>
    /// 提交事务过程中发送错误,通过AllExceptions获取哪个服务发生错误
    /// </summary>
    public class TransactionCommitArrayException : Exception
    {
        public List<TransactionCommitException> AllExceptions { get; }
        public TransactionCommitArrayException(List<TransactionCommitException> allExceptions,string message):base(message)
        {
            AllExceptions = allExceptions;
        }
    }
}
