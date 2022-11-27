using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Applications
{
    interface IRequestHandler
    {
        InvokeType MatchType { get; }
        Task Handle(NetClient netclient, InvokeCommand cmd);
    }
}
