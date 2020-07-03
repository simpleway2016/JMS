using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS.Impls
{
    class RollbackRequestHandler : IRollbackRequestHandler
    {
        TransactionDelegateCenter _transactionDelegateCenter;
        public RollbackRequestHandler(TransactionDelegateCenter transactionDelegateCenter)
        {
            _transactionDelegateCenter = transactionDelegateCenter;
        }
        public void Handle(NetStream netclient, InvokeCommand cmd)
        {
            _transactionDelegateCenter.Rollback(cmd.Header["TranId"]);
            netclient.WriteServiceData(new InvokeResult() { Success = true });
        }
    }
}
