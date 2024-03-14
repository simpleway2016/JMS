﻿using System;
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
        /// 附加属性
        /// </summary>
        public Dictionary<string, string> Properties;

        /// <summary>
        /// 服务器是否启用了ssl证书
        /// </summary>
        public bool UseSsl;
        public RegisterServiceLocation(string address, int port)
        {
            this.Port = port;
            this.Host = this.ServiceAddress = address;
        }

        public RegisterServiceLocation(NetAddress address)
        {
            this.Port = address.Port;
            this.Host = this.ServiceAddress = address.Address;
        }

        public RegisterServiceLocation()
        {

        }
    }
   
    public class RegisterServiceInfo: RegisterServiceLocation
    {
        /// <summary>
        /// 支持的服务
        /// </summary>
        [Obsolete]
        public string[] ServiceNames;
        public ServiceDetail[] ServiceList;
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
        /// 用来是否运行客户端连接的检验代码的文件路径
        /// </summary>
        public string ClientCheckCodeFile;
      
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
        public int RequestQuantity;
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
        public bool UseSsl;
        /// <summary>
        /// 支持的服务
        /// </summary>
        public ServiceDetail[] ServiceList;
        /// <summary>
        /// 本机最多同时并发几个线程
        /// </summary>
        public int MaxThread;
        public string ServiceId;
        public int MaxRequestCount;
        /// <summary>
        /// 附加属性
        /// </summary>
        public Dictionary<string,string> Properties;
        /// <summary>
        /// 当前微服务的负载情况
        /// </summary>
        public PerformanceInfo PerformanceInfo;
    }
}
