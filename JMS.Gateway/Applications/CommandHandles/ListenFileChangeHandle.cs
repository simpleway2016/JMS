using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class ListenFileChangeHandler : ICommandHandler
    {
        ListenFileChangeReception _listenFileChangeReception;

        public ListenFileChangeHandler(ListenFileChangeReception listenFileChangeReception)
        {
            this._listenFileChangeReception = listenFileChangeReception;

        }
        public CommandType MatchCommandType => CommandType.ListenFileChange;

        public Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            return _listenFileChangeReception.Handle(netclient, cmd);

        }
    }
}
