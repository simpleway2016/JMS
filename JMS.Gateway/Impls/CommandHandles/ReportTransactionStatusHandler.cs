using JMS.Interfaces;
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

namespace JMS.Impls.CommandHandles
{
    class ReportTransactionStatusHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        TransactionStatusManager _TransactionStatusManager;
        public ReportTransactionStatusHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _TransactionStatusManager = _serviceProvider.GetService<TransactionStatusManager>();
        }
        public CommandType MatchCommandType => CommandType.ReportTransactionStatus;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var tran = cmd.Content;
            _TransactionStatusManager.AddSuccessTransaction(tran);

            netclient.WriteServiceData(new InvokeResult
            {
                Success = true
            });
        }
    }

}
