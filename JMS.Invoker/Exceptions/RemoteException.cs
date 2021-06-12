using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class MissServiceException : Exception
    {
        public MissServiceException(string message):base(message)
        {
        }
    }
}
