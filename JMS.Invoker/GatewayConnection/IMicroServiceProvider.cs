using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.GatewayConnection
{
    internal interface IMicroServiceProvider
    {
        RegisterServiceRunningInfo GetServiceLocation(string serviceName);
    }

}
