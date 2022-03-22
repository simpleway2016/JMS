using JMS.RetryCommit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS
{
    /// <summary>
    /// 管理不能自我管理的事务
    /// </summary>
    class TransactionDelegateCenter
    {
        FaildCommitBuilder _faildCommitBuilder;
        ILogger<TransactionDelegateCenter> _logger;
        ILogger<TransactionDelegate> _loggerTran;
        public List<TransactionDelegate> List { get; set; }
        public TransactionDelegateCenter(ILogger<TransactionDelegateCenter> logger, ILogger<TransactionDelegate> loggerTran, FaildCommitBuilder faildCommitBuilder)
        {
            this._faildCommitBuilder = faildCommitBuilder;
            List = new List<TransactionDelegate>();
            _logger = logger;
            _loggerTran = loggerTran;
            new Thread(checkTimeoutTransaction).Start();
        }

        void checkTimeoutTransaction()
        {
            while(true)
            {
                try
                {
                    for (int i = 0; i < List.Count; i++)
                    {
                        var delegateItem = List[i];
                        if (delegateItem != null && (DateTime.Now - delegateItem.InCenterTime).TotalSeconds >= 30 )
                        {
                            //大于30秒没有处理的事务，马上回滚
                            try
                            {
                                lock (delegateItem)
                                {
                                    if (delegateItem.Handled == false)
                                    {
                                        delegateItem.RollbackAction?.Invoke();
                                        delegateItem.Handled = true;

                                        if (delegateItem.RetryCommitFilePath != null)
                                        {
                                            _faildCommitBuilder.Timeout(delegateItem.RetryCommitFilePath);
                                            delegateItem.RetryCommitFilePath = null;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, ex.Message);
                            }

                            _logger?.LogInformation("超时未处理，自动回滚事务，原始请求:{0}",delegateItem.RequestCommand?.ToJsonString());
                            lock (List)
                            {
                                List.Remove(delegateItem);
                            }
                            i--;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                }


                if (List.Count == 0)
                {
                    Thread.Sleep(10000);
                }
                else
                    Thread.Sleep(2000);
            }
        }

        /// <summary>
        /// 添加事务委托
        /// </summary>
        /// <param name="tranDelegate"></param>
        public void AddTransactionDelegate(TransactionDelegate tranDelegate)
        {
            tranDelegate.InCenterTime = DateTime.Now;
            lock (List)
            {
                List.Add(tranDelegate);
            }
        }

        public bool Commit(string tranId)
        {
            for(int i = 0; i < List.Count; i ++)
            {
                var delegateItem = List[i];
                if(delegateItem != null && delegateItem.TransactionId == tranId)
                {
                    try
                    {
                        lock (delegateItem)
                        {
                            if (delegateItem.Handled)
                                return false;

                            if(delegateItem.CommitAction != null)
                            {
                                delegateItem.CommitAction();
                                if(delegateItem.RetryCommitFilePath != null)
                                {
                                    _faildCommitBuilder.CommitSuccess(delegateItem.RetryCommitFilePath);
                                    delegateItem.RetryCommitFilePath = null;
                                }
                                _loggerTran?.LogInformation("事务{0}提交完毕，请求数据:{1}", delegateItem.TransactionId, delegateItem.RequestCommand.ToJsonString());
                            }
                          
                            delegateItem.Handled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, ex.Message);
                        throw ex;
                    }
                    finally
                    {
                        lock (List)
                        {
                            List.Remove(delegateItem);
                        }
                    }

                   
                    return true;
                }
            }

            return false;
        }

        public bool Rollback(string tranId)
        {
            for (int i = 0; i < List.Count; i++)
            {
                var delegateItem = List[i];
                if (delegateItem != null && delegateItem.TransactionId == tranId)
                {
                    try
                    {
                        lock (delegateItem)
                        {
                            if (delegateItem.Handled)
                                return false;

                            if (delegateItem.RollbackAction != null)
                            {
                                delegateItem.RollbackAction();
                                if (delegateItem.RetryCommitFilePath != null)
                                {
                                    _faildCommitBuilder.Rollback(delegateItem.RetryCommitFilePath);
                                    delegateItem.RetryCommitFilePath = null;
                                }
                                _loggerTran?.LogInformation("事务{0}回滚完毕，请求数据:{1}", delegateItem.TransactionId, delegateItem.RequestCommand.ToJsonString());
                            }
                            delegateItem.Handled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, ex.Message);
                        throw ex;
                    }
                    finally
                    {
                        lock (List)
                        {
                            List.Remove(delegateItem);
                        }
                    }
                   
                    return true;
                }
            }

            return false;
        }

        public void RollbackAll()
        {
            TransactionDelegate[] items = null;
            lock (List)
            {
                items = List.ToArray();
                List.Clear();
            }

            foreach( var delegateItem in items)
            {
                if (delegateItem != null)
                {
                    try
                    {
                        lock (delegateItem)
                        {
                            if (delegateItem.Handled == false)
                            {
                                if (delegateItem.RollbackAction != null)
                                {
                                    delegateItem.RollbackAction();
                                    if (delegateItem.RetryCommitFilePath != null)
                                    {
                                        _faildCommitBuilder.Rollback(delegateItem.RetryCommitFilePath);
                                        delegateItem.RetryCommitFilePath = null;
                                    }
                                    _loggerTran?.LogInformation("事务{0}回滚完毕，请求数据:{1}", delegateItem.TransactionId, delegateItem.RequestCommand.ToJsonString());
                                }
                                delegateItem.Handled = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, ex.Message);
                    }

                }
            }
        }
    }
}
