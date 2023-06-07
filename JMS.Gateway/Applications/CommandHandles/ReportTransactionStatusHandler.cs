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
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class ReportTransactionStatusHandler : ICommandHandler
    {
        TransactionStatusManager _transactionStatusManager;
        public ReportTransactionStatusHandler(TransactionStatusManager transactionStatusManager)
        {
            this._transactionStatusManager = transactionStatusManager;
        }
        public CommandType MatchCommandType => CommandType.ReportTransactionStatus;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var tran = cmd.Content;
            _transactionStatusManager.AddSuccessTransaction(tran);

            netclient.WriteServiceData(new InvokeResult
            {
                Success = true
            });
        }
    }

}
