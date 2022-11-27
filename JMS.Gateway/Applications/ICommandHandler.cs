using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Applications
{
    interface ICommandHandler
    {
        CommandType MatchCommandType { get; }
        Task Handle(NetClient netclient,GatewayCommand cmd);
    }
}
