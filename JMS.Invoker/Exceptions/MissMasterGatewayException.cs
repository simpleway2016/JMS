using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class MissMasterGatewayException : RemoteException
    {
        public MissMasterGatewayException(string message):base(null,message)
        {

        }
    }
}
