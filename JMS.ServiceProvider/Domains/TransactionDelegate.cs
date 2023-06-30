using JMS.Domains;
using JMS.Dtos;
using JMS.RetryCommit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Way.Lib;
using static JMS.RetryCommit.FaildCommitBuilder;

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
        private readonly MicroServiceControllerBase _controller;
        private IStorageEngine _storageEngine;

        public string TransactionId { get; }
        public string TransactionFlag { get; }
        public IStorageEngine StorageEngine => _storageEngine;

        /// <summary>
        /// 如果在最后想阻止统一提交事务，可以把AgreeCommit设为false，那么，所有事务最后将回滚
        /// </summary>
        public bool AgreeCommit { get; set; }

        public Action CommitAction { get; set; }
        public Action RollbackAction { get; set; }
        internal bool Handled { get; set; }

        public bool SupportTransaction => _storageEngine != null || CommitAction != null || RollbackAction != null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="controller">当前Controller</param>
        /// <param name="storageEngine">数据库对象</param>
        public TransactionDelegate(MicroServiceControllerBase controller, IStorageEngine storageEngine)
        {
            _controller = controller;
            _storageEngine = storageEngine;
            this.AgreeCommit = true;
            this.TransactionId = controller.TransactionId;
            if (controller.Headers.ContainsKey("TranFlag"))
            {
                this.TransactionFlag = controller.Headers["TranFlag"];
            }
        }

        [Obsolete("建议使用另一个构造函数：TransactionDelegate(MicroServiceControllerBase controller, IStorageEngine storageEngine)\r\n 这样CommitAction、RollbackAction可以不再赋值")]
        public TransactionDelegate(MicroServiceControllerBase controller)
        {
            _controller = controller;
            this.AgreeCommit = true;
            this.TransactionId = controller.TransactionId;
            if (controller.Headers.ContainsKey("TranFlag"))
            {
                this.TransactionFlag = controller.Headers["TranFlag"];
            }
        }


        public void CommitTransaction()
        {
            if (_storageEngine != null)
            {
                _storageEngine.CommitTransaction();
                _storageEngine = null;
            }
            else if (CommitAction != null)
            {
                CommitAction();
                CommitAction = null;
            }
        }

        public void RollbackTransaction()
        {
            if (_storageEngine != null)
            {
                _storageEngine.RollbackTransaction();
                _storageEngine = null;
            }
            else if (RollbackAction != null)
            {
                RollbackAction();
                RollbackAction = null;
            }
        }

        internal async Task<InvokeCommand> WaitForCommandAsync(List<TransactionDelegate> transactionDelegateList, IGatewayConnector gatewayConnector, FaildCommitBuilder faildCommitBuilder, NetClient netclient, ILogger logger)
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
                        case (int)InvokeType.Invoke:
                            return cmd;
                        case (int)InvokeType.CommitTranaction:
                            onCommit(transactionDelegateList, gatewayConnector, faildCommitBuilder, netclient, logger);
                            return null;
                        case (int)InvokeType.RollbackTranaction:
                            onRollback(transactionDelegateList, gatewayConnector, faildCommitBuilder, netclient, logger);
                            return null;
                        case (int)InvokeType.HealthyCheck:
                            checkedHealth = true;
                            onHealthyCheck(transactionDelegateList , gatewayConnector, faildCommitBuilder, netclient, logger);
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
                    if (transactionDelegateList == null || _storageEngine == null || transactionDelegateList.Any(m => m.StorageEngine == _storageEngine) == false)
                    {
                        transactionDelegateList.CommitTransaction();
                        this.CommitTransaction();
                    }
                    else
                    {
                        transactionDelegateList.CommitTransaction();
                    }
                    

                    if (this.RetryCommitFilePath != null)
                    {
                        faildCommitBuilder.CommitSuccess(this.RetryCommitFilePath);
                        this.RetryCommitFilePath = null;
                    }
                }
                else
                {
                    //网关告知事务失败，需要回滚事务

                    _controller.ServiceProvider.GetService<ILogger<TransactionDelegate>>()?.LogInformation($"事务{this.TransactionId}回滚");

                    if (transactionDelegateList == null || _storageEngine == null || transactionDelegateList.Any(m => m.StorageEngine == _storageEngine) == false)
                    {
                        this.RollbackTransaction();
                    }
                    transactionDelegateList.RollbackTransaction();

                    if (this.RetryCommitFilePath != null)
                    {
                        faildCommitBuilder.UnkonwStatus(this.RetryCommitFilePath);
                        this.RetryCommitFilePath = null;
                    }
                }
            }

            return null;
        }

        void onCommit(List<TransactionDelegate> transactionDelegateList, IGatewayConnector gatewayConnector, FaildCommitBuilder faildCommitBuilder, NetClient netclient, ILogger logger)
        {
            try
            {
              
                if (transactionDelegateList == null || _storageEngine == null || transactionDelegateList.Any(m => m.StorageEngine == _storageEngine) == false)
                {
                    transactionDelegateList.CommitTransaction();
                    this.CommitTransaction();
                }
                else
                {
                    transactionDelegateList.CommitTransaction();
                }
               

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
                _storageEngine = null;
            }

            netclient.WriteServiceData(new InvokeResult() { Success = true });
        }
        void onRollback(List<TransactionDelegate> transactionDelegateList, IGatewayConnector gatewayConnector, FaildCommitBuilder faildCommitBuilder, NetClient netclient, ILogger logger)
        {
            if (transactionDelegateList == null || _storageEngine == null || transactionDelegateList.Any(m => m.StorageEngine == _storageEngine) == false)
            {
                transactionDelegateList.RollbackTransaction();
                this.RollbackTransaction();
            }
            else
            {
                transactionDelegateList.RollbackTransaction();
            }
         

            if (RetryCommitFilePath != null)
            {
                faildCommitBuilder.Rollback(RetryCommitFilePath);
                RetryCommitFilePath = null;
                //logger?.LogInformation("事务{0}回滚完毕，请求数据:{1}", TransactionId, RequestCommand.ToJsonString());
            }

            netclient.WriteServiceData(new InvokeResult() { Success = true });
        }

        string getUserContentString(object userContent)
        {
            if (userContent != null && userContent is System.Security.Claims.ClaimsPrincipal claimsPrincipal)
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    claimsPrincipal.WriteTo(new System.IO.BinaryWriter(ms));
                    ms.Position = 0;
                    userContent = ms.ToArray();
                }
            }
            return userContent?.ToJsonString();
        }
        void onHealthyCheck(List<TransactionDelegate> transactionDelegateList, IGatewayConnector gatewayConnector, FaildCommitBuilder faildCommitBuilder, NetClient netclient, ILogger logger)
        {
            if (_storageEngine != null || CommitAction != null)
            {
                if (transactionDelegateList != null && transactionDelegateList.Count > 0)
                {
                    var list = (from m in transactionDelegateList
                     select new RequestInfo
                     {
                         Cmd = m.RequestCommand,
                         TransactionId = m.TransactionId,
                         TransactionFlag = m.TransactionFlag,
                         UserContentType = m.UserContent == null ? null : m.UserContent.GetType(),
                         UserContentValue = m.UserContent == null ? null : getUserContentString(m.UserContent)
                     }).ToList();

                    list.Add(new RequestInfo {
                        Cmd = this.RequestCommand,
                        TransactionId = this.TransactionId,
                        TransactionFlag = this.TransactionFlag,
                        UserContentType = this.UserContent == null ? null : this.UserContent.GetType(),
                        UserContentValue = this.UserContent == null ? null : getUserContentString(this.UserContent)
                    });

                    RetryCommitFilePath = faildCommitBuilder.Build( this.TransactionId, list);
                }
                else
                {
                    var info = new RequestInfo {
                        Cmd = this.RequestCommand,
                        TransactionId = this.TransactionId,
                        TransactionFlag = this.TransactionFlag,
                        UserContentType = this.UserContent == null ? null : this.UserContent.GetType(),
                        UserContentValue = this.UserContent == null ? null : getUserContentString(this.UserContent)
                    };
                    RetryCommitFilePath = faildCommitBuilder.Build(info);
                }
            }
            //logger?.LogInformation("准备提交事务{0}，请求数据:{1},身份验证信息:{2}", TransactionId, RequestCommand.ToJsonString(), UserContent);
            netclient.WriteServiceData(new InvokeResult
            {
                Success = AgreeCommit
            });
        }
    }

    public static class TransactionDelegateExtension
    {
        public static void CommitTransaction(this List<TransactionDelegate> list)
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    item.CommitTransaction();
                }
            }
        }

        public static void RollbackTransaction(this List<TransactionDelegate> list)
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    item.RollbackTransaction();
                }
            }
        }
    }
}
