using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public class InvokeAttributes
    {
        public int? StatusCode { get; set; }
    }
    internal interface IInvokeConnect : IDisposable
    {
        InvokingInformation InvokingInfo { get; }
        Invoker Invoker { get; }
        /// <summary>
        /// 是否有事务正在挂起
        /// </summary>
        bool HasTransactionHolding { get; }

        Task<T> InvokeAsync<T>(string method, RemoteClient tran, params object[] parameter);
        Task<InvokeResult<T>> InvokeExAsync<T>(string method, RemoteClient tran, params object[] parameter);
        T Invoke<T>(string method, RemoteClient tran, params object[] parameters);

        /// <summary>
        /// 确认对方是否可以提交事务
        /// </summary>
        /// <returns></returns>
        InvokeResult GoReadyCommit(RemoteClient tran);
        InvokeResult GoCommit(RemoteClient tran);
        InvokeResult GoRollback(RemoteClient tran);

        Task<InvokeResult> GoReadyCommitAsync(RemoteClient tran);
        Task<InvokeResult> GoCommitAsync(RemoteClient tran);
        Task<InvokeResult> GoRollbackAsync(RemoteClient tran);

        /// <summary>
        /// 重新执行事务
        /// </summary>
        /// <param name="proxyAddress">代理服务地址，可为null</param>
        /// <param name="serviceLocation"></param>
        /// <param name="cert">连接服务器的证书</param>
        /// <param name="tranId"></param>
        void RetryTranaction(NetAddress proxyAddress, ClientServiceDetail serviceLocation,byte[] cert, string tranId,string tranFlag);

        void AddClientToPool();
    }
}
