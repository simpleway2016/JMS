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
        public static event EventHandler<RegisterServiceInfo> MicroServiceUploadLockedKeyCompleted;
        public static event EventHandler<string> ShareFileChanged;

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

        public static void OnMicroServiceUploadLockedKeyCompleted(RegisterServiceInfo serviceInfo)
        {
            if (MicroServiceUploadLockedKeyCompleted != null)
                MicroServiceUploadLockedKeyCompleted(null, serviceInfo);
        }
        public static void OnShareFileChanged(string file)
        {
            if (ShareFileChanged != null)
                ShareFileChanged(null, file);
        }

    }
}
