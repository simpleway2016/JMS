using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Domains
{
    public interface IMicroServiceReception
    {
        RegisterServiceInfo ServiceInfo { get; set; }
        Task HealthyCheck(NetClient netclient, GatewayCommand registerCmd);
        void Close();
    }
}
