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

namespace JMS.Applications.CommandHandles
{
    class GetTransactionStatusHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        TransactionStatusManager _TransactionStatusManager;
        public GetTransactionStatusHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _TransactionStatusManager = _serviceProvider.GetService<TransactionStatusManager>();
        }
        public CommandType MatchCommandType => CommandType.GetTransactionStatus;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var tran = cmd.Content;

            netclient.WriteServiceData(new InvokeResult
            {
                Success = _TransactionStatusManager.IsTransactionSuccess(tran)
            }) ;
        }
    }

}
