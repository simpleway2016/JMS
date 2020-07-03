using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface IMicroServiceReception
    {
        void HealthyCheck(Way.Lib.NetStream netclient, GatewayCommand registerCmd);
    }
}
