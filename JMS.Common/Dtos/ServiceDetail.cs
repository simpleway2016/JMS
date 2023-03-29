using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Dtos
{
    public enum ServiceType
    {
        WebApi = 1,
        WebSocket = 2,
        JmsService = 3
    }
    public class ServiceDetail
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 服务描述
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 是否允许通过网关反向代理访问此服务器
        /// 默认可以通过 http://网关域名/服务名称/ 来访问
        /// </summary>
        public bool AllowGatewayProxy { get; set; }
        public ServiceType Type { get; set; }
    }
}
