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
    class GetTransactionStatusHandler : ICommandHandler
    {
        TransactionStatusManager _transactionStatusManager;
        public GetTransactionStatusHandler(TransactionStatusManager transactionStatusManager)
        {
            this._transactionStatusManager = transactionStatusManager;
        }
        public CommandType MatchCommandType => CommandType.GetTransactionStatus;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var tran = cmd.Content;

            netclient.WriteServiceData(new InvokeResult
            {
                Success = _transactionStatusManager.IsTransactionSuccess(tran)
            }) ;
        }
    }

}
