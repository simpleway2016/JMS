using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface ICommandHandler
    {
        CommandType MatchCommandType { get; }
        void Handle(Way.Lib.NetStream netclient,GatewayCommand cmd);
    }
}
