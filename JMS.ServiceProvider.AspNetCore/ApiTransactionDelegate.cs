using JMS.Domains;
using JMS.Dtos;
using JMS.RetryCommit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS.ServiceProvider.AspNetCore
{
    public class ApiTransactionDelegate
    {
        internal static AsyncLocal<string> CurrentTranId = new AsyncLocal<string>();
        internal InvokeInfo InvokeInfo;
        internal object UserContent;
        internal string RetryCommitFilePath;
        /// <summary>
        /// 事务标识，如果为null，表示本次访问来自浏览器等常规http客户端
        /// </summary>
        public string TransactionId { get; }
        public ApiTransactionDelegate()
        {
            this.AgreeCommit = true;
            this.TransactionId = CurrentTranId.Value;
            CurrentTranId.Value = null;
        }
        
        /// <summary>
        /// 如果在最后想阻止统一提交事务，可以把AgreeCommit设为false，那么，所有事务最后将回滚
        /// </summary>
        public bool AgreeCommit { get; set; }

        public Action CommitAction { get; set; }
        public Action RollbackAction { get; set; }
        internal bool Handled { get; set; }


        internal void WaitForCommand(IGatewayConnector gatewayConnector, ApiFaildCommitBuilder faildCommitBuilder, NetClient netClient, ILogger logger)
        {
            string cmd;
            try
            {
                while (true)
                {
                    cmd = netClient.ReadServiceData();
                    switch (cmd)
                    {
                        case "commit":
                            onCommit(gatewayConnector, faildCommitBuilder, netClient, logger);
                            return;
                        case "rollback":
                            onRollback(gatewayConnector, faildCommitBuilder, netClient, logger);
                            return;
                        case "ready":
                            onHealthyCheck(gatewayConnector, faildCommitBuilder, netClient, logger);
                            break;
                    }
                }
            }
            catch (SocketException)
            {
                Thread.Sleep(2000);//延迟2秒，问问网关事务提交情况
                if (gatewayConnector.CheckTransaction(this.TransactionId))
                {
                    //网关告知事务已成功，需要提交事务
                    this.CommitAction();

                    if (this.RetryCommitFilePath != null)
                    {
                        faildCommitBuilder.CommitSuccess(this.RetryCommitFilePath);
                        this.RetryCommitFilePath = null;
                    }
                }
                else
                {
                    //网关告知事务识别，需要回滚事务
                    this.RollbackAction();

                    if (this.RetryCommitFilePath != null)
                    {
                        faildCommitBuilder.UnkonwStatus(this.RetryCommitFilePath);
                        this.RetryCommitFilePath = null;
                    }
                }
            }
        }

        void onCommit(IGatewayConnector gatewayConnector, ApiFaildCommitBuilder faildCommitBuilder, NetClient netClient, ILogger logger)
        {
            try
            {
                CommitAction?.Invoke();
                if (RetryCommitFilePath != null)
                {
                    faildCommitBuilder.CommitSuccess(RetryCommitFilePath);
                    RetryCommitFilePath = null;
                }
            }
            catch (Exception ex)
            {
                if (RetryCommitFilePath != null)
                {
                    faildCommitBuilder.CommitFaild(RetryCommitFilePath);
                    RetryCommitFilePath = null;
                }
                logger?.LogInformation("事务{0}提交失败,{1}", TransactionId, ex.Message);
                throw ex;
            }
            finally
            {
                CommitAction = null;
            }

            logger?.LogInformation("事务{0}提交完毕", TransactionId);

            netClient.WriteServiceData( Encoding.UTF8.GetBytes("ok"));
        }
        void onRollback(IGatewayConnector gatewayConnector, ApiFaildCommitBuilder faildCommitBuilder, NetClient netClient, ILogger logger)
        {
            RollbackAction?.Invoke();
            RollbackAction = null;
            if (RetryCommitFilePath != null)
            {
                faildCommitBuilder.Rollback(RetryCommitFilePath);
                RetryCommitFilePath = null;
            }
            logger?.LogInformation("事务{0}回滚完毕，请求数据:{1}", TransactionId, this.InvokeInfo.ToJsonString());

            netClient.WriteServiceData(Encoding.UTF8.GetBytes("ok"));
        }
        void onHealthyCheck(IGatewayConnector gatewayConnector, ApiFaildCommitBuilder faildCommitBuilder, NetClient netClient, ILogger logger)
        {
            if (CommitAction != null)
            {
                RetryCommitFilePath = faildCommitBuilder.Build(TransactionId, this.InvokeInfo,this.UserContent);
            }
            logger?.LogInformation("准备提交事务{0}，请求数据:{1},身份验证信息:{2}", TransactionId, this.InvokeInfo.ToJsonString(), UserContent);
            netClient.WriteServiceData(Encoding.UTF8.GetBytes("ok"));
        }
    }
}
