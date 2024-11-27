using JMS.Dtos;
using JMS.RetryCommit;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JMS.Applications
{
    class RetryTranactionHandler : IRequestHandler
    {
        ILogger<RetryTranactionHandler> _logger;
        RetryCommitMission _retryCommitMission;

        public RetryTranactionHandler(RetryCommitMission retryCommitMission, ILogger<RetryTranactionHandler> logger)
        {
            this._logger = logger;
            this._retryCommitMission = retryCommitMission;

        }

        public InvokeType MatchType => InvokeType.RetryTranaction;

        public async Task Handle(NetClient netclient, InvokeCommand cmd)
        {
            _logger.LogDebug($"客户端要求重新执行事务：{cmd.ToJsonString()}");
            var tranid = cmd.Header["TranId"];
            string tranFlag = null;
            if (cmd.Header.ContainsKey("TranFlag"))
                tranFlag = cmd.Header["TranFlag"];

           
            var ret = _retryCommitMission.RetryTranaction(tranid , tranFlag);
            _logger.LogDebug($"执行完毕，结果：{ret}");
            netclient.WriteServiceData(new InvokeResult
            {
                Success = true,
                Data = ret // 
            });
        }
    }
}
