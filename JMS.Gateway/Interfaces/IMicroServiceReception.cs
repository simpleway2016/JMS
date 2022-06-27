using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    public interface IMicroServiceReception
    {
        RegisterServiceInfo ServiceInfo { get; set; }
        void HealthyCheck(NetClient netclient, GatewayCommand registerCmd);
        void Close();
    }
}
