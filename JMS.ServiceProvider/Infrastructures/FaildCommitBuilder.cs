using JMS.Dtos;
using JMS.Infrastructures;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Way.Lib;

namespace JMS.RetryCommit
{
    class FaildCommitBuilder
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
        public string Build(RequestInfo requestInfo)
        {
            try
            {
                if (Directory.Exists(_microServiceHost.RetryCommitPath) == false)
                    Directory.CreateDirectory(_microServiceHost.RetryCommitPath);

              
                var filepath = $"{_microServiceHost.RetryCommitPath}/{requestInfo.TransactionId}_{Guid.NewGuid().ToString("N")}.txt";
                File.WriteAllText(filepath, requestInfo.ToJsonString(), Encoding.UTF8);
                return filepath;
            }
            catch (Exception ex)
            {
                _loggerTran.LogError(ex, "FaildCommitBuilder");
                return null;
            }
        }

        public string Build(string transactionId, IEnumerable<RequestInfo> requestInfos)
        {
            try
            {
                if (Directory.Exists(_microServiceHost.RetryCommitPath) == false)
                    Directory.CreateDirectory(_microServiceHost.RetryCommitPath);


                var filepath = $"{_microServiceHost.RetryCommitPath}/{transactionId}_{Guid.NewGuid().ToString("N")}.txt";
                File.WriteAllText(filepath, requestInfos.ToJsonString(), Encoding.UTF8);
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
            public string TransactionFlag;
            public string TransactionId;
            public InvokeCommand Cmd;
            public Type UserContentType;
           
            public string UserContentValue;

            public ClaimsPrincipal GetUserContent( ILogger logger)
            {
                if (this.UserContentValue != null)
                {

                    try
                    {
                        if (this.UserContentType == typeof(System.Security.Claims.ClaimsPrincipal))
                        {

                            byte[] bs = this.UserContentValue.FromJson<byte[]>();
                            using (var ms = new System.IO.MemoryStream(bs))
                            {
                                return new System.Security.Claims.ClaimsPrincipal(new BinaryReader(ms));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError("RetryCommitMission无法还原事务id为{0}的身份信息,{1}", this.TransactionId, ex.Message);

                    }


                }
                return null;
            }
        }
    }


}
