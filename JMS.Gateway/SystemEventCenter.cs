using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    class SystemEventCenter
    {
        public static event EventHandler<RegisterServiceInfo> MicroServiceOnline;
        public static event EventHandler<RegisterServiceInfo> MicroServiceOnffline;

        public static void OnMicroServiceOnline(RegisterServiceInfo serviceInfo)
        {
            if (MicroServiceOnline != null)
                MicroServiceOnline(null, serviceInfo);
        }

        public static void OnMicroServiceOnffline(RegisterServiceInfo serviceInfo)
        {
            if (MicroServiceOnffline != null)
                MicroServiceOnffline(null, serviceInfo);
        }
    }
}
