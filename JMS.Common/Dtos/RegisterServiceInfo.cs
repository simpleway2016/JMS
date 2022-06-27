using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace JMS.Dtos
{
    public class RegisterServiceLocation
    {
        public int Port;
        /// <summary>
        /// 微服务的ip地址
        /// </summary>
        public string Host;
        /// <summary>
        /// 对外提供微服务的地址
        /// </summary>
        public string ServiceAddress;
        /// <summary>
        /// 
        /// </summary>
        public string TransactionId;
        /// <summary>
        /// 微服务的自定义描述
        /// </summary>
        public string Description;
    }
   
    public class RegisterServiceInfo: RegisterServiceLocation
    {
        /// <summary>
        /// 支持的服务
        /// </summary>
        public string[] ServiceNames;
        /// <summary>
        /// 本机最多同时并发几个线程
        /// </summary>
        public int MaxThread;
        /// <summary>
        /// 
        /// </summary>
        public string ServiceId;

        /// <summary>
        /// 最多允许多少个请求数，0表示无限制。
        /// 网关版本>=1.1.0.0支持此功能
        /// </summary>
        public int MaxRequestCount;


        /// <summary>
        /// 当前请求数量
        /// </summary>
        public int RequestQuantity;
        /// <summary>
        /// cpu使用率
        /// </summary>
        public double CpuUsage;

        /// <summary>
        /// 用来是否运行客户端连接的检验代码
        /// </summary>
        public string ClientCheckCode;
        /// <summary>
        /// 是否同一时间只有一个相同的服务器运行（双机热备）
        /// </summary>
        public bool SingletonService;
    }

    public class PerformanceInfo
    {
        /// <summary>
        /// 当前连接的请求数
        /// </summary>
        public int? RequestQuantity;
        /// <summary>
        /// CPU利用率
        /// </summary>
        public double? CpuUsage;
    }

    public class RegisterServiceRunningInfo
    {
        public int Port;
        public string Host;
        public string ServiceAddress;
        /// <summary>
        /// 支持的服务
        /// </summary>
        public string[] ServiceNames;
        /// <summary>
        /// 本机最多同时并发几个线程
        /// </summary>
        public int MaxThread;
        public string ServiceId;
        public string Description;
        /// <summary>
        /// 当前微服务的负载情况
        /// </summary>
        public PerformanceInfo PerformanceInfo;
    }
}
