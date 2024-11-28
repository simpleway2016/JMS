using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using JMS.Dtos;
using System.Threading.Tasks;
using JMS.Cluster;
using JMS.Common.Collections;

namespace JMS.Applications.CommandHandles
{
    class GetServiceProviderHandler : ICommandHandler
    {
        IServiceProviderAllocator _serviceProviderAllocator;
        ClusterGatewayConnector _clusterGatewayConnector;
        public GetServiceProviderHandler(ClusterGatewayConnector clusterGatewayConnector, IServiceProviderAllocator serviceProviderAllocator)
        {
            this._serviceProviderAllocator = serviceProviderAllocator;
            this._clusterGatewayConnector = clusterGatewayConnector;
        }
        public CommandType MatchCommandType => CommandType.GetServiceProvider;

        void outputResult(NetClient netclient, GatewayCommand cmd , ClientServiceDetail location)
        {
            if (cmd.IsHttp)
            {
                if (location == null)
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
                if (location == null)
                {
                    location = new ClientServiceDetail("", 0);
                }
                netclient.WriteServiceData(location);
            }
        }

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            if (cmd.Header == null)
            {
                cmd.Header = new IgnoreCaseDictionary();
            }
            var requestBody = cmd.Content.FromJson<GetServiceProviderRequest>();
            requestBody.Header = cmd.Header;
            requestBody.ClientAddress = ((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString();
            try
            {
                if (_clusterGatewayConnector.IsMaster == false)
                {
                    outputResult(netclient, cmd, new ClientServiceDetail("not master", 0));
                }
                else
                {

                    var location = _serviceProviderAllocator.Alloc(requestBody);
                    if(location == null)
                    {
                        outputResult(netclient, cmd,null);
                        return;
                    }

                    outputResult(netclient, cmd, location);
                }
            }
            catch
            {
                outputResult(netclient, cmd, null);
            }

        }
    }
}
