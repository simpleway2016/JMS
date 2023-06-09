using JMS.Domains;
using JMS.Dtos;
using JMS.RetryCommit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    /// <summary>
    /// 事务委托
    /// </summary>
    public class TransactionDelegate
    {
        internal InvokeCommand RequestCommand;
        internal string RetryCommitFilePath;
        internal object UserContent;
        public string TransactionId { get; }
       public string TransactionFlag { get; }
        public TransactionDelegate(MicroServiceControllerBase controller)
        {
            this.AgreeCommit = true;
            this.TransactionId = controller.TransactionId;
            if (controller.Headers.ContainsKey("TranFlag"))
            {
                this.TransactionFlag = controller.Headers["TranFlag"];
            }
        }

        /// <summary>
        /// 如果在最后想阻止统一提交事务，可以把AgreeCommit设为false，那么，所有事务最后将回滚
        /// </summary>
        public bool AgreeCommit { get; set; }

        public Action CommitAction { get; set; }
        public Action RollbackAction { get; set; }
        internal bool Handled { get; set; }


        internal async Task WaitForCommandAsync(IGatewayConnector gatewayConnector, FaildCommitBuilder faildCommitBuilder, NetClient netclient,ILogger logger)
        {
            InvokeCommand cmd;
            bool checkedHealth = false;
            try
            {
                while (true)
                {
                    cmd = await netclient.ReadServiceObjectAsync<InvokeCommand>();
                    switch (cmd.Type)
                    {
                        case (int)InvokeType.CommitTranaction:
                            onCommit(gatewayConnector, faildCommitBuilder, netclient,logger);
                            return;
                        case (int)InvokeType.RollbackTranaction:
                            onRollback(gatewayConnector, faildCommitBuilder, netclient, logger);
                            return;
                        case (int)InvokeType.HealthyCheck:
                            checkedHealth = true;
                            onHealthyCheck(gatewayConnector, faildCommitBuilder, netclient, logger);
                            break;
                    }
                }
            }
            catch (SocketException)
            {
                if (checkedHealth)
                {
                    Thread.Sleep(2000);//延迟2秒，问问网关事务提交情况
                }

                if (checkedHealth && await gatewayConnector.CheckTransactionAsync(this.TransactionId))
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
                    //网关告知事务失败，需要回滚事务
                    this.RollbackAction();

                    if (this.RetryCommitFilePath != null)
                    {
                        faildCommitBuilder.UnkonwStatus(this.RetryCommitFilePath);
                        this.RetryCommitFilePath = null;
                    }
                }
            }
        }

        void onCommit(IGatewayConnector gatewayConnector, FaildCommitBuilder faildCommitBuilder, NetClient netclient, ILogger logger)
        {
            try
            {
                CommitAction?.Invoke();
                if (RetryCommitFilePath != null)
                {
                    faildCommitBuilder.CommitSuccess(RetryCommitFilePath);
                    RetryCommitFilePath = null;
                    //logger?.LogInformation("事务{0}提交完毕", TransactionId);
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

            netclient.WriteServiceData(new InvokeResult() { Success = true });
        }
        void onRollback(IGatewayConnector gatewayConnector, FaildCommitBuilder faildCommitBuilder, NetClient netclient, ILogger logger)
        {
            RollbackAction?.Invoke();
            RollbackAction = null;
            if (RetryCommitFilePath != null)
            {
                faildCommitBuilder.Rollback(RetryCommitFilePath);
                RetryCommitFilePath = null;
                //logger?.LogInformation("事务{0}回滚完毕，请求数据:{1}", TransactionId, RequestCommand.ToJsonString());
            }
           
            netclient.WriteServiceData(new InvokeResult() { Success = true });
        }
        void onHealthyCheck(IGatewayConnector gatewayConnector, FaildCommitBuilder faildCommitBuilder, NetClient netclient, ILogger logger)
        {
            if (CommitAction != null)
            {
                RetryCommitFilePath = faildCommitBuilder.Build(TransactionId,TransactionFlag, RequestCommand, UserContent);
            }
            //logger?.LogInformation("准备提交事务{0}，请求数据:{1},身份验证信息:{2}", TransactionId, RequestCommand.ToJsonString(), UserContent);
            netclient.WriteServiceData(new InvokeResult
            {
                Success = AgreeCommit
            });
        }
    }
}
