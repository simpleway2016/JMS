using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Dtos
{
    public class ClientServiceDetail : ServiceDetail
    {
        public int Port;
        /// <summary>
        /// 对外提供微服务的地址
        /// </summary>
        public string ServiceAddress;
        /// <summary>
        /// 服务器是否启用了ssl证书
        /// </summary>
        public bool UseSsl;

        public ClientServiceDetail(string address, int port)
        {
            this.Port = port;
            this.ServiceAddress = address;
        }

        public ClientServiceDetail(NetAddress address)
        {
            this.Port = address.Port;
            this.ServiceAddress = address.Address;
        }

        public ClientServiceDetail(ServiceDetail source,RegisterServiceLocation registerServiceLocation)
        {
            this.Name = source.Name;
            this.AllowGatewayProxy = source.AllowGatewayProxy;
            this.Type = source.Type;
            this.Description = source.Description;
            this.Port = registerServiceLocation.Port;
            this.ServiceAddress = registerServiceLocation.ServiceAddress;
            this.UseSsl = registerServiceLocation.UseSsl;
        }
        public ClientServiceDetail(ServiceDetail source, RegisterServiceRunningInfo registerServiceRunningInfo)
        {
            this.Name = source.Name;
            this.AllowGatewayProxy = source.AllowGatewayProxy;
            this.Type = source.Type;
            this.Description = source.Description;
            this.Port = registerServiceRunningInfo.Port;
            this.ServiceAddress = registerServiceRunningInfo.ServiceAddress;
            this.UseSsl = registerServiceRunningInfo.UseSsl;
        }
        public ClientServiceDetail()
        {

        }
    }
}
