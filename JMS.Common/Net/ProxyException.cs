using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Common.Net
{
    public class ProxyException : Exception
    {
        public ProxyException(string message):base(message) { }
    }
}
