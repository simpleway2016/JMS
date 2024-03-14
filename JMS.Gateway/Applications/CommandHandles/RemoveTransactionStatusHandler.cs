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
        TransactionStatusManager _transactionStatusManager;
        public RemoveTransactionStatusHandler(TransactionStatusManager transactionStatusManager)
        {
            this._transactionStatusManager = transactionStatusManager;
        }
        public CommandType MatchCommandType => CommandType.RemoveTransactionStatus;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var tran = cmd.Content;
            _transactionStatusManager.RemoveTransaction(tran);
            netclient.WriteServiceData(new InvokeResult
            {
                Success = true
            }) ;
        }
    }

}
