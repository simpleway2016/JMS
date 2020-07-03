using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface IRollbackRequestHandler
    {
        void Handle(Way.Lib.NetStream netclient,InvokeCommand cmd);
    }
}
