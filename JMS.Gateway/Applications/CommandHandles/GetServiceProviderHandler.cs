using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using JMS.Dtos;
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class GetServiceProviderHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        ClusterGatewayConnector _gatewayRefereeClient;
        public GetServiceProviderHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _gatewayRefereeClient = serviceProvider.GetService<ClusterGatewayConnector>();
        }
        public CommandType MatchCommandType => CommandType.GetServiceProvider;

        void outputResult(NetClient netclient, GatewayCommand cmd , RegisterServiceLocation location)
        {
            if (cmd.IsHttp)
            {
                if (location.Host.Length == 0)
                {
                    var contentBytes = Encoding.UTF8.GetBytes("{}");
                    netclient.OutputHttpContent(contentBytes);
                }
                else
                {
                    var contentBytes = Encoding.UTF8.GetBytes(location.ToJsonString());
                    netclient.OutputHttpContent(contentBytes);
                }
            }
            else
            {
                netclient.WriteServiceData(location);
            }
        }

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            if (cmd.Header == null)
            {
                cmd.Header = new Dictionary<string, string>();
            }
            var requestBody = cmd.Content.FromJson<GetServiceProviderRequest>();
            requestBody.Header = cmd.Header;
            requestBody.ClientAddress = ((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString();
            try
            {
                if (_gatewayRefereeClient.IsMaster == false)
                {
                    outputResult( netclient,cmd, new RegisterServiceLocation
                    {
                        Host = "not master",
                        Port = 0
                    });
                }
                else
                {

                    var location = _serviceProvider.GetService<IServiceProviderAllocator>().Alloc(requestBody);
                    if(location == null)
                    {
                        outputResult(netclient, cmd, new RegisterServiceLocation
                        {
                            Host = "",
                            Port = 0
                        });
                        return;
                    }

                    outputResult(netclient, cmd, location);
                }
            }
            catch
            {
                outputResult(netclient, cmd, new RegisterServiceLocation
                {
                    Host = "",
                    Port = 0
                });
            }

        }
    }
}
