using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface IMicroServiceReception
    {
        RegisterServiceInfo ServiceInfo { get; set; }
        int Id { get; }
        void HealthyCheck(NetClient netclient, GatewayCommand registerCmd);
    }
}
