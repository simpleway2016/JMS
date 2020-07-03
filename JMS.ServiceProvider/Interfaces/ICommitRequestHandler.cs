using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface ICommitRequestHandler
    {
        void Handle(Way.Lib.NetStream netclient,InvokeCommand cmd);
    }
}
