using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMS.InvokeConnects
{
    internal class InvokeConnectFactory
    {
        public static IInvokeConnect Create(IRemoteClient remoteClient, string serviceName,ClientServiceDetail serviceLocation,Invoker invoker)
        {
            if (remoteClient != null)
            {
                RemoteClient client = (RemoteClient)remoteClient;
                if (client._SupportTransaction && client._Connects.Count > 0)
                {
                    var matchItem = client._Connects.FirstOrDefault(m => m.InvokingInfo.ServiceLocation.ServiceAddress == serviceLocation.ServiceAddress && m.InvokingInfo.ServiceLocation.Port == serviceLocation.Port);
                    if (matchItem != null)
                    {
                        return matchItem;
                    }
                }
            }

            IInvokeConnect ret = null;
            if (serviceLocation.Port == 0)
            {
                ret = new HttpInvokeConnect(serviceName, serviceLocation, invoker);
            }
            else
            {
                ret = new InvokeConnect(serviceName, serviceLocation, invoker);
            }

            remoteClient.AddConnect(ret);
            return ret;
        }
    }
}
