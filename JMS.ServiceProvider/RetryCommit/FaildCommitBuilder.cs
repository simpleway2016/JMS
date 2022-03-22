using JMS.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Way.Lib;

namespace JMS.RetryCommit
{
    internal class FaildCommitBuilder
    {
        RetryCommitMission _retryCommitMission;
        ILogger<TransactionDelegate> _loggerTran;
        MicroServiceHost _microServiceHost;

        public FaildCommitBuilder(MicroServiceHost microServiceHost,
            RetryCommitMission retryCommitMission,
            ILogger<TransactionDelegate> loggerTran)
        {
            this._retryCommitMission = retryCommitMission;
            this._loggerTran = loggerTran;
            this._microServiceHost = microServiceHost;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="transactionId">事务id</param>
        /// <param name="cmd">请求对象</param>
        /// <param name="userContent">身份信息对象</param>
        public string Build(string transactionId, InvokeCommand cmd, object userContent)
        {
            if (Directory.Exists(_microServiceHost.RetryCommitPath) == false)
                Directory.CreateDirectory(_microServiceHost.RetryCommitPath);

            var filepath = $"{_microServiceHost.RetryCommitPath}/{transactionId}_{DateTime.Now.Ticks}.txt";
            File.WriteAllText(filepath, new RequestInfo
            {
                Cmd = cmd,
                TransactionId = transactionId,
                UserContentType = userContent?.GetType(),
                UserContentValue = userContent?.ToJsonString()
            }.ToJsonString(), Encoding.UTF8);
            return filepath;
        }

        public void CommitSuccess(string filepath)
        {
            try
            {
                File.Delete(filepath);
            }
            catch (Exception ex)
            {
                _loggerTran?.LogError("删除RetryCommitFilePath失败，{0} {1}", filepath, ex.Message);
            }
        }
        public void Rollback(string filepath)
        {
            try
            {
                File.Delete(filepath);
            }
            catch (Exception ex)
            {
                _loggerTran?.LogError("删除RetryCommitFilePath失败，{0} {1}", filepath, ex.Message);
            }
        }

        public void CommitFaild(string filepath)
        {
            try
            {
                File.Move(filepath, filepath + ".err");
            }
            catch (Exception ex)
            {
                _loggerTran?.LogError("移动RetryCommitFilePath失败，{0} {1}", filepath, ex.Message);
            }
        }
        public void Timeout(string filepath)
        {
            try
            {
                File.Move(filepath, filepath + ".timeout");
            }
            catch (Exception ex)
            {
                _loggerTran?.LogError("移动RetryCommitFilePath失败，{0} {1}", filepath, ex.Message);
            }
        }
        internal class RequestInfo
        {
            public string TransactionId;
            public InvokeCommand Cmd;
            public Type UserContentType;
            public string UserContentValue;
        }
    }


}
