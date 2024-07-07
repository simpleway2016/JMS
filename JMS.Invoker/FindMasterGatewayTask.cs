using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks.Sources;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using JMS.Dtos;
using JMS.GatewayConnection;
using System.Net.Mail;

namespace JMS
{
    public class FindMasterGatewayTask
    {
        public bool SupportRemoteConnection;
        private readonly NetAddress[] _gatewayAddrs;
        private readonly int _timeout;
        private readonly NetAddress _proxyAddr;
        int _done = 0;
        /// <summary>
        /// 0 waiting  1=success 2=error  3=tobeSuccess
        /// </summary>
        int _status = 0;

        Exception _lastError = null;

        public FindMasterGatewayTask(NetAddress[] gatewayAddrs, int timeout, NetAddress proxyAddr)
        {
            _gatewayAddrs = gatewayAddrs;
            _timeout = timeout;
            _proxyAddr = proxyAddr;
        }

        public async Task<NetAddress> GetMasterAsync()
        {            
            var taskCompletionSource = new TaskCompletionSource<NetAddress>();
            var totalCount = _gatewayAddrs.Length;

            foreach (var addr in _gatewayAddrs)
            {
                if (_status == 1)
                    break;

                tryConnect(addr, totalCount , taskCompletionSource);

            }

            return await taskCompletionSource.Task;
        }



        async void tryConnect(NetAddress addr, int totalCount, TaskCompletionSource<NetAddress> taskCompletionSource)
        {
            NetClient client = null;
            try
            {
                client = await NetClientPool.CreateClientAsync(_proxyAddr, addr);
                client.ReadTimeout = _timeout;
                client.WriteServiceData(new GatewayCommand
                {
                    Type = (int)CommandType.FindMaster
                });
                var ret = await client.ReadServiceObjectAsync<InvokeResult<FindMasterResult>>();
                NetClientPool.AddClientToPool(client);

                if (ret.Success == true)
                {
                    if (Interlocked.CompareExchange(ref _status, 3, 0) == 0)
                    {
                        SupportRemoteConnection = ret.Data != null && ret.Data.SupportRetmoteClientConnect;
                   
                        taskCompletionSource.TrySetResult(addr);
                        _status = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                _lastError = ex;
                client?.Dispose();
            }
            finally
            {
                var ret = Interlocked.Increment(ref _done);

                if (_lastError != null && ret == totalCount)
                {
                    taskCompletionSource.TrySetException(new MissMasterGatewayException("无法找到主网关"));
                }
            }
        }

    }

}
