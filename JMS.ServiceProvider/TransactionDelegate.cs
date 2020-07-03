using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    /// <summary>
    /// 事务委托
    /// </summary>
    public class TransactionDelegate
    {
        internal InvokeCommand RequestCommand;
        public string TransactionId { get; }
        public TransactionDelegate(string tranId)
        {
            this.TransactionId = tranId;
        }
        internal DateTime InCenterTime;
        public Action CommitAction { get; set; }
        public Action RollbackAction { get; set; }
    }
}
