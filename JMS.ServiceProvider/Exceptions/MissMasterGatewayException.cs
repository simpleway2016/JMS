using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class MissMasterGatewayException : Exception
    {
        public MissMasterGatewayException(string msg):base(msg)
        {

        }
    }
}
