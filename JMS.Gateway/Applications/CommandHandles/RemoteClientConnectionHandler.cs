using JMS.Domains;
using JMS.Dtos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    internal class RemoteClientConnectionHandler : ICommandHandler
    {
        public CommandType MatchCommandType =>  CommandType.RemoteClientConnection;
        IRemoteClientManager _remoteClientManager;
        public RemoteClientConnectionHandler(IServiceProvider serviceProvider)
        {
            _remoteClientManager = serviceProvider.GetService<IRemoteClientManager>();
        }
        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            _remoteClientManager.AddRemoteClient(netclient);
            try
            {
                while (true)
                {
                    await netclient.ReadServiceObjectAsync<GatewayCommand>();
                }
            }
            catch
            {
                _remoteClientManager.RemoveRemoteClient(netclient);
            }
        }
    }
}
