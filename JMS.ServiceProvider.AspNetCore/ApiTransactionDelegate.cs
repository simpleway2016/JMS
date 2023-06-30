using JMS.Domains;
using JMS.Dtos;
using JMS.RetryCommit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;
using static JMS.ServiceProvider.AspNetCore.ApiFaildCommitBuilder;

namespace JMS.ServiceProvider.AspNetCore
{
    public class ApiTransactionDelegate
    {
        internal static AsyncLocal<(string tranId, string tranFlag)> CurrentTranId = new AsyncLocal<(string tranId, string tranFlag)>();
        internal InvokeInfo InvokeInfo;
        internal object UserContent;
        internal string RetryCommitFilePath;
        /// <summary>
        /// 事务标识，如果为null，表示本次访问来自浏览器等常规http客户端
        /// </summary>
        public string TransactionId { get; internal set; }
        public string TransactionFlag { get; internal set; }
        IStorageEngine _storageEngine;
        public ApiTransactionDelegate()
        {
            this.AgreeCommit = true;
            var val = CurrentTranId.Value;
            this.TransactionId = val.tranId;
            this.TransactionFlag = val.tranFlag;
            CurrentTranId.Value = (null, null);
        }

        /// <summary>
        /// 如果在最后想阻止统一提交事务，可以把AgreeCommit设为false，那么，所有事务最后将回滚
        /// </summary>
        public bool AgreeCommit { get; set; }

        public Action CommitAction { get; set; }
        public Action RollbackAction { get; set; }

        public bool SupportTransaction => _storageEngine != null || CommitAction != null || RollbackAction != null;
        public IStorageEngine StorageEngine
        {
            get
            {
                return _storageEngine;
            }
            set
            {
                _storageEngine = value;
            }
        }

        internal void Clear()
        {
            _storageEngine = null;
            CommitAction = null;
            RollbackAction = null;
            AgreeCommit = true;
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

        internal async Task<string> WaitForCommandAsync(List<ApiTransactionDelegate> transactionDelegateList, IGatewayConnector gatewayConnector, ApiFaildCommitBuilder faildCommitBuilder, NetClient netClient, ILogger logger)
        {
            string cmd;
            bool checkedHealth = false;
            try
            {
                while (true)
                {
                    cmd = await netClient.ReadServiceDataAsync();
                    switch (cmd)
                    {
                        case "invoke":
                            return await netClient.ReadServiceDataAsync();
                        case "commit":
                            onCommit( transactionDelegateList, gatewayConnector, faildCommitBuilder, netClient, logger);
                            return null;
                        case "rollback":
                            onRollback(transactionDelegateList, gatewayConnector, faildCommitBuilder, netClient, logger);
                            return null;
                        case "ready":
                            checkedHealth = true;
                            onHealthyCheck( transactionDelegateList, gatewayConnector, faildCommitBuilder, netClient, logger);
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
                    //网关告知事务识别，需要回滚事务
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

        void onCommit(List<ApiTransactionDelegate> transactionDelegateList, IGatewayConnector gatewayConnector, ApiFaildCommitBuilder faildCommitBuilder, NetClient netClient, ILogger logger)
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

            //logger?.LogInformation("事务{0}提交完毕", TransactionId);

            netClient.WriteServiceData( Encoding.UTF8.GetBytes("ok"));
        }
        void onRollback(List<ApiTransactionDelegate> transactionDelegateList, IGatewayConnector gatewayConnector, ApiFaildCommitBuilder faildCommitBuilder, NetClient netClient, ILogger logger)
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
            }
            //logger?.LogInformation("事务{0}回滚完毕，请求数据:{1}", TransactionId, this.InvokeInfo.ToJsonString());

            netClient.WriteServiceData(Encoding.UTF8.GetBytes("ok"));
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

        void onHealthyCheck(List<ApiTransactionDelegate> transactionDelegateList, IGatewayConnector gatewayConnector, ApiFaildCommitBuilder faildCommitBuilder, NetClient netClient, ILogger logger)
        {
            if (_storageEngine != null || CommitAction != null)
            {
                if (transactionDelegateList != null && transactionDelegateList.Count > 0)
                {
                    var list = (from m in transactionDelegateList
                                select new RequestInfo
                                {
                                    Cmd = m.InvokeInfo,
                                    TransactionId = m.TransactionId,
                                    TransactionFlag = m.TransactionFlag,
                                    UserContentType = m.UserContent == null ? null : m.UserContent.GetType(),
                                    UserContentValue = m.UserContent == null ? null : getUserContentString(m.UserContent)
                                }).ToList();

                    list.Add(new RequestInfo
                    {
                        Cmd = this.InvokeInfo,
                        TransactionId = this.TransactionId,
                        TransactionFlag = this.TransactionFlag,
                        UserContentType = this.UserContent == null ? null : this.UserContent.GetType(),
                        UserContentValue = this.UserContent == null ? null : getUserContentString(this.UserContent)
                    });

                    RetryCommitFilePath = faildCommitBuilder.Build(this.TransactionId, list);
                }
                else
                {
                    var info = new RequestInfo
                    {
                        Cmd = this.InvokeInfo,
                        TransactionId = this.TransactionId,
                        TransactionFlag = this.TransactionFlag,
                        UserContentType = this.UserContent == null ? null : this.UserContent.GetType(),
                        UserContentValue = this.UserContent == null ? null : getUserContentString(this.UserContent)
                    };
                    RetryCommitFilePath = faildCommitBuilder.Build(info);
                }
            }

            //logger?.LogInformation("准备提交事务{0}，请求数据:{1},身份验证信息:{2}", TransactionId, this.InvokeInfo.ToJsonString(), UserContent);
            netClient.WriteServiceData(Encoding.UTF8.GetBytes("ok"));
        }
    }

    public static class ApiTransactionDelegateExtension
    {
        public static void CommitTransaction(this List<ApiTransactionDelegate> list)
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    item.CommitTransaction();
                }
            }
        }

        public static void RollbackTransaction(this List<ApiTransactionDelegate> list)
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
