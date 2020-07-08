using JMS.GenerateCode;
using JMS.Impls;
using JMS.Interfaces;
using JMS.ScheduleTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;
using System.Reflection;
using JMS.Common.Dtos;

namespace JMS
{
    public class MicroServiceOption
    {
        /// <summary>
        /// 微服务端口
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// 把事务发起时的远程调用参数保存在指定目录，默认为null，表示不保存
        /// </summary>
        public string TransactionLogFolder { get; set; }
        /// <summary>
        /// 网关地址
        /// </summary>
        public NetAddress[] GatewayAddresses { get; set; }
    }
}
