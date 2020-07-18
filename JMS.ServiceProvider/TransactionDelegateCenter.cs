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
        ILogger<TransactionDelegateCenter> _logger;
        public List<TransactionDelegate> List { get; set; }
        public TransactionDelegateCenter(ILogger<TransactionDelegateCenter> logger)
        {
            List = new List<TransactionDelegate>();
            _logger = logger;
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
                    lock (delegateItem)
                    {
                        if (delegateItem.Handled)
                            return false;

                        delegateItem.CommitAction?.Invoke();
                        delegateItem.Handled = true;                        
                    }

                    lock (List)
                    {
                        List.Remove(delegateItem);
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
                    lock (delegateItem)
                    {
                        if (delegateItem.Handled)
                            return false;

                        delegateItem.RollbackAction?.Invoke();
                        delegateItem.Handled = true;                       
                    }

                    lock (List)
                    {
                        List.Remove(delegateItem);
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
                                delegateItem.RollbackAction?.Invoke();
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
