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
            string tranFlag = null;
            if (cmd.Header.ContainsKey("TranFlag"))
                tranFlag = cmd.Header["TranFlag"];

            var ret = _retryCommitMission.RetryTranaction(tranid , tranFlag);
            netclient.WriteServiceData(new InvokeResult
            {
                Success = true,
                Data = ret // 
            });
        }
    }
}
