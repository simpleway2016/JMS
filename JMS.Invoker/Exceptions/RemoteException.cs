using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class RemoteException:Exception
    {
        public RemoteException(string message):base(message)
        {

        }
    }
}
