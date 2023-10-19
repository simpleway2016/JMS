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
        IRemoteClientManager _remoteClientManager;
        public CommandType MatchCommandType => CommandType.RemoteClientConnection;
        public RemoteClientConnectionHandler(IRemoteClientManager remoteClientManager)
        {
            this._remoteClientManager = remoteClientManager;
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
