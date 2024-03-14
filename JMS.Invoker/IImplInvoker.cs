using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class InvokerInfoAttribute:Attribute
    {
        public string ServiceName { get; }
        public InvokerInfoAttribute(string serviceName)
        {
            this.ServiceName = serviceName;
        }
    }
    public interface IImplInvoker
    {
    }
}
