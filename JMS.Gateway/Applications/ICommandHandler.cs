using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Applications
{
    interface ICommandHandler
    {
        CommandType MatchCommandType { get; }
        void Handle(NetClient netclient,GatewayCommand cmd);
    }
}
