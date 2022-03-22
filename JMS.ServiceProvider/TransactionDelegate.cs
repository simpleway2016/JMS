using JMS.Dtos;
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
        internal string RetryCommitFilePath;
        public string TransactionId { get; }
        public TransactionDelegate(string tranId)
        {
            this.AgreeCommit = true;
            this.TransactionId = tranId;
        }
        internal DateTime InCenterTime;
        /// <summary>
        /// 如果在最后想阻止统一提交事务，可以把AgreeCommit设为false，那么，所有事务最后将回滚
        /// </summary>
        public bool AgreeCommit { get; set; }

        public Action CommitAction { get; set; }
        public Action RollbackAction { get; set; }
        internal bool Handled { get; set; }

    }
}
