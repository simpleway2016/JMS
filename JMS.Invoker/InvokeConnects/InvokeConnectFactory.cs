using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.InvokeConnects
{
    internal class InvokeConnectFactory
    {
        public static IInvokeConnect Create(string serviceName,ClientServiceDetail serviceLocation,Invoker invoker)
        {
            if(serviceLocation.Port == 0)
            {
                return new HttpInvokeConnect(serviceName, serviceLocation, invoker);
            }
            else
            {
                return new InvokeConnect(serviceName, serviceLocation, invoker);
            }
        }
    }
}
