using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class TransactionControl
    {
        Way.Lib.NetStream _netclient;
        internal TransactionControl(Way.Lib.NetStream netclient)
        {
            _netclient = netclient;
        }
    }
}
