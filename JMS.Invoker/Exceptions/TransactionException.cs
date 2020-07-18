
using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS
{
    /// <summary>
    /// 提交事务过程中发送错误
    /// </summary>
    public class TransactionException : Exception
    {
        public RegisterServiceLocation ServiceLocation { get; }
        public TransactionException(RegisterServiceLocation serviceLocation, string message) : base(message + $" 微服务器位置：" + serviceLocation.ToJsonString())
        {
            ServiceLocation = serviceLocation;
        }
    }
}