﻿
using JMS.Common.Json;
using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    /// <summary>
    /// 提交事务过程中发送错误
    /// </summary>
    public class TransactionException : Exception
    {
        public InvokingInformation InvokingInfo { get; }
        public TransactionException(InvokingInformation invokeInfo, string message) : base($"{message} 详细请求信息：{ApplicationJsonSerializer.JsonSerializer.Serialize(invokeInfo)}" )
        {
            InvokingInfo = invokeInfo;
        }
    }
}