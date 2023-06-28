using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS.GatewayConnection
{
    internal interface IMicroServiceProvider
    {
        Task<ClientServiceDetail> GetServiceLocationAsync(RemoteClient tran, string serviceName);
        ClientServiceDetail GetServiceLocation(RemoteClient tran, string serviceName);
    }

}
