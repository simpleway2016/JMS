using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.Domains
{
    internal class TransactionStatusManager
    {
        Jack.Storage.MemoryList.StorageContext<TransactionStatusDataItem> _dataContext;
        public event EventHandler<string> TransactionSuccess;
        public event EventHandler<string> TransactionRemove;
        public TransactionStatusManager()
        {
            _dataContext = new Jack.Storage.MemoryList.StorageContext<TransactionStatusDataItem>("TransactionStatus","Tran");
            new Thread(checkForDelete).Start();
        }

        void checkForDelete()
        {
            while(true)
            {
                var time = DateTime.Now.AddDays(-10);
                foreach ( var item in _dataContext )
                {
                    if(item.CreateTime < time)
                    {
                        _dataContext.Remove(item);
                    }
                }
                Thread.Sleep(600000);
            }
        }

        public void AddSuccessTransaction(string tran)
        {
            _dataContext.AddOrUpdate(new TransactionStatusDataItem { 
                Tran = tran,
                CreateTime = DateTime.Now,
            });
            if(TransactionSuccess != null)
            {
                TransactionSuccess(this, tran);
            }
        }
        public void RemoveTransaction(string tran)
        {
            var item = _dataContext.FirstOrDefault(m => m.Tran == tran);
            if (item != null)
                _dataContext.Remove(item);
            if (TransactionRemove != null)
            {
                TransactionRemove(this, tran);
            }
        }
        /// <summary>
        /// 查询事务是否已经成功
        /// </summary>
        /// <param name="tran"></param>
        /// <returns></returns>
        public bool IsTransactionSuccess(string tran)
        {
            return _dataContext.Any(m => m.Tran == tran);
        }

    }

    class TransactionStatusDataItem
    {
        public string Tran { get; set; }
        public DateTime CreateTime { get; set; }
    }
}
