using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

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
                                delegateItem.RollbackAction?.Invoke();
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, ex.Message);
                            }

                            _logger?.LogInformation("超时未处理，自动回滚事务，事务id:{0}",delegateItem.TransactionId);
                            lock (List)
                            {
                                List.RemoveAt(i);
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

        public void Commit(string tranId)
        {
            for(int i = 0; i < List.Count; i ++)
            {
                var delegateItem = List[i];
                if(delegateItem != null && delegateItem.TransactionId == tranId)
                {
                    delegateItem.CommitAction?.Invoke();
                    lock (List)
                    {
                        List.RemoveAt(i);
                    }
                    return;
                }
            }

            throw new Exception("找不到指定事务，可能事务已经回滚");
        }

        public void Rollback(string tranId)
        {
            for (int i = 0; i < List.Count; i++)
            {
                var delegateItem = List[i];
                if (delegateItem != null && delegateItem.TransactionId == tranId)
                {
                    delegateItem.RollbackAction?.Invoke();
                    lock (List)
                    {
                        List.RemoveAt(i);
                    }
                    break;
                }
            }
        }

        public void RollbackAll()
        {
            for (int i = 0; i < List.Count; i++)
            {
                var delegateItem = List[i];
                if (delegateItem != null)
                {
                    try
                    {
                        delegateItem.RollbackAction?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, ex.Message);
                    }

                    lock (List)
                    {
                        List.RemoveAt(i);
                    }
                    i--;
                }
            }
        }
    }
}
