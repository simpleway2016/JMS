using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMS.InvokeConnects
{
    internal class InvokeConnectFactory
    {
        public static IInvokeConnect Create(RemoteClient remoteClient, string serviceName,ClientServiceDetail serviceLocation,Invoker invoker)
        {
            if (remoteClient != null)
            {
                if (remoteClient._SupportTransaction && remoteClient._Connects.Count > 0)
                {
                    var matchItem = remoteClient._Connects.FirstOrDefault(m => m.InvokingInfo.ServiceLocation.IsTheSameServer(serviceLocation));
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

            if (remoteClient != null)
            {
                remoteClient.AddConnect(ret);
            }
            return ret;
        }
    }

    internal static class LocationExtension
    {
        public static bool IsTheSameServer(this ClientServiceDetail clientServiceDetail , ClientServiceDetail otherLocation)
        {
            if (clientServiceDetail == otherLocation)
                return true;

            if(clientServiceDetail.Port == 0 && otherLocation.Port == 0)
            {
                var uri1 = new Uri(clientServiceDetail.ServiceAddress);
                var uri2 = new Uri(otherLocation.ServiceAddress);
                return uri1.Port == uri2.Port && uri1.Host == uri2.Host;
            }
            else
            {
                return clientServiceDetail.ServiceAddress == otherLocation.ServiceAddress && clientServiceDetail.Port == otherLocation.Port;
            }
        }
        public static bool IsTheSameServer(this ClientServiceDetail clientServiceDetail, string otherAddr,int port)
        {
            if (clientServiceDetail.Port == 0 && port == 0)
            {
                var uri1 = new Uri(clientServiceDetail.ServiceAddress);
                var uri2 = new Uri(otherAddr);
                return uri1.Port == uri2.Port && uri1.Host == uri2.Host;
            }
            else
            {
                return clientServiceDetail.ServiceAddress == otherAddr && clientServiceDetail.Port == port;
            }
        }
    }
}
