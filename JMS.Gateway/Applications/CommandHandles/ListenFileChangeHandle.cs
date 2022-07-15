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

namespace JMS.Applications.CommandHandles
{
    class ListenFileChangeHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _gateway;
        
        public ListenFileChangeHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _gateway = _serviceProvider.GetService<Gateway>();
        }
        public CommandType MatchCommandType => CommandType.ListenFileChange;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            _serviceProvider.GetService<ListenFileChangeReception>().Handle(netclient, cmd);

        }
    }
}
