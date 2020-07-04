using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using JMS.Dtos;

namespace JMS.Impls.CommandHandles
{
    class GetServiceProviderHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        TransactionIdBuilder _TransactionIdBuilder;
        public GetServiceProviderHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _TransactionIdBuilder = serviceProvider.GetService<TransactionIdBuilder>();
        }
        public CommandType MatchCommandType => CommandType.GetServiceProvider;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            if (cmd.Header.ContainsKey("TranId") == false)
            {
                cmd.Header["TranId"] = _TransactionIdBuilder.Build();
            }
            var requestBody = cmd.Content.FromJson<GetServiceProviderRequest>();
            requestBody.Header = cmd.Header;
            requestBody.ClientAddress = ((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString();
            try
            {
                var location = _serviceProvider.GetService<IServiceProviderAllocator>().Alloc(requestBody);
                location.TransactionId = cmd.Header["TranId"];
                netclient.WriteServiceData(location);
            }
            catch
            {
                netclient.WriteServiceData(new RegisterServiceLocation
                {
                    Host = "",
                    Port = 0,
                    TransactionId = cmd.Header["TranId"]
                });
            }

        }
    }
}
