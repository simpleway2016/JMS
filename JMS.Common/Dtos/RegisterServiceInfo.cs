using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace JMS.Dtos
{
    public class RegisterServiceLocation
    {
        public int Port;
        public string Host;
        public string TransactionId;
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
        public string ServiceId;

    }

    public class RegisterServiceRunningInfo
    {
        public int Port;
        public string Host;
        /// <summary>
        /// 支持的服务
        /// </summary>
        public string[] ServiceNames;
        /// <summary>
        /// 本机最多同时并发几个线程
        /// </summary>
        public int MaxThread;
        public string ServiceId;
        /// <summary>
        /// 当前连接的请求数
        /// </summary>
        public int? ClientConnected;
    }
}
