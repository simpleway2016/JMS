using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Common
{
    public class SizeLimitException : Exception
    {
        public SizeLimitException(string message):base(message) { }
    }
}
