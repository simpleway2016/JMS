using JMS.HttpProxy.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Attributes
{
    public class ProxyTypeAttribute : Attribute
    {
        public ProxyTypeAttribute(ProxyType proxyType)
        {
            ProxyType = proxyType;
        }

        public ProxyType ProxyType { get; }
    }
}
