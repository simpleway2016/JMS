using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    internal interface IInvokeConnect : IDisposable
    {
        InvokingInformation InvokingInfo { get; }
        Invoker Invoker { get; }

        Task<T> InvokeAsync<T>(string method, IRemoteClient tran, params object[] parameter);
        T Invoke<T>(string method, IRemoteClient tran, params object[] parameters);

        /// <summary>
        /// 确认对方是否可以提交事务
        /// </summary>
        /// <returns></returns>
        InvokeResult GoReadyCommit(IRemoteClient tran);
        InvokeResult GoCommit(IRemoteClient tran);
        InvokeResult GoRollback(IRemoteClient tran);

        /// <summary>
        /// 重新执行事务
        /// </summary>
        /// <param name="proxyAddress">代理服务地址，可为null</param>
        /// <param name="serviceLocation"></param>
        /// <param name="cert">连接服务器的证书</param>
        /// <param name="tranId"></param>
        void RetryTranaction(NetAddress proxyAddress, RegisterServiceLocation serviceLocation,byte[] cert, string tranId,string tranFlag);

        void AddClientToPool();
    }
}
