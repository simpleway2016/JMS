using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using JMS.RetryCommit;

namespace JMS.Applications
{
    class RetryTranactionHandler : IRequestHandler
    {
        RetryCommitMission _retryCommitMission;

        public RetryTranactionHandler(RetryCommitMission retryCommitMission)
        {
            this._retryCommitMission = retryCommitMission;

        }

        public InvokeType MatchType => InvokeType.RetryTranaction;

        public void Handle(NetClient netclient, InvokeCommand cmd)
        {
            var tranid = cmd.Header["TranId"];
            var ret = _retryCommitMission.RetryTranaction(tranid);
            netclient.WriteServiceData(new InvokeResult
            {
                Success = true,
                Data = ret // 
            });
        }
    }
}
