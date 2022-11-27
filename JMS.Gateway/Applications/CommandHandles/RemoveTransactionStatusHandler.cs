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
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class RemoveTransactionStatusHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        TransactionStatusManager _TransactionStatusManager;
        public RemoveTransactionStatusHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _TransactionStatusManager = _serviceProvider.GetService<TransactionStatusManager>();
        }
        public CommandType MatchCommandType => CommandType.RemoveTransactionStatus;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var tran = cmd.Content;
            _TransactionStatusManager.RemoveTransaction(tran);
            netclient.WriteServiceData(new InvokeResult
            {
                Success = true
            }) ;
        }
    }

}
