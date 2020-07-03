using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS.Impls
{
    class CommitRequestHandler : ICommitRequestHandler
    {
        TransactionDelegateCenter _transactionDelegateCenter;
        public CommitRequestHandler(TransactionDelegateCenter transactionDelegateCenter)
        {
            _transactionDelegateCenter = transactionDelegateCenter;
        }
        public void Handle(NetStream netclient, InvokeCommand cmd)
        {
            _transactionDelegateCenter.Commit(cmd.Header["TranId"]);
            netclient.WriteServiceData(new InvokeResult() { Success = true });
        }
    }
}
