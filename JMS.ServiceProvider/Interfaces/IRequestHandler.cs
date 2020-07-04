using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS.Interfaces
{
    interface IRequestHandler
    {
        InvokeType MatchType { get; }
        void Handle(NetClient netclient, InvokeCommand cmd);
    }
}
