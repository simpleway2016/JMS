using JMS.Dtos;
using JMS.Infrastructures;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Way.Lib;

namespace JMS.ServiceProvider.AspNetCore
{
    class ApiFaildCommitBuilder
    {
        ApiRetryCommitMission _retryCommitMission;
        ILogger<TransactionDelegate> _loggerTran;
        MicroServiceHost _microServiceHost;

        public ApiFaildCommitBuilder(MicroServiceHost microServiceHost,
            ApiRetryCommitMission retryCommitMission,
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
        public string Build(string transactionId, InvokeInfo cmd, object userContent)
        {
            try
            {
                if (Directory.Exists(_microServiceHost.RetryCommitPath) == false)
                    Directory.CreateDirectory(_microServiceHost.RetryCommitPath);

                Type userContentType = userContent?.GetType();
                if (userContent != null && userContent is System.Security.Claims.ClaimsPrincipal claimsPrincipal)
                {
                    using (var ms = new System.IO.MemoryStream())
                    {
                        claimsPrincipal.WriteTo(new System.IO.BinaryWriter(ms));
                        ms.Position = 0;
                        userContent = ms.ToArray();
                    }
                }

                var filepath = $"{_microServiceHost.RetryCommitPath}/{transactionId}_{Guid.NewGuid().ToString("N")}.txt";
                File.WriteAllText(filepath, new RequestInfo
                {
                    Cmd = cmd,
                    TransactionId = transactionId,
                    UserContentType = userContent == null ? null : userContentType,
                    UserContentValue = userContent?.ToJsonString()
                }.ToJsonString(), Encoding.UTF8);
                return filepath;
            }
            catch (Exception ex)
            {
                _loggerTran.LogError(ex, "FaildCommitBuilder");
                return null;
            }
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
                FileHelper.ChangeFileExt(filepath, ".err");
            }
            catch (Exception ex)
            {
                _loggerTran?.LogError("移动RetryCommitFilePath失败，{0} {1}", filepath, ex.Message);
            }
        }

        /// <summary>
        /// 保存至未知状态
        /// </summary>
        /// <param name="filepath"></param>
        public void UnkonwStatus(string filepath)
        {
            try
            {
                FileHelper.ChangeFileExt(filepath, ".unknow");
            }
            catch (Exception ex)
            {
                _loggerTran?.LogError("移动RetryCommitFilePath失败，{0} {1}", filepath, ex.Message);
            }
        }

        internal class RequestInfo
        {
            public string TransactionId;
            public InvokeInfo Cmd;
            public Type UserContentType;
            public string UserContentValue;
        }
    }


}
