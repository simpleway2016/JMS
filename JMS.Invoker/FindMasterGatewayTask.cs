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
    class FindMasterGatewayTask
    {
        public bool SupportRemoteConnection;
        private readonly NetAddress[] _gatewayAddrs;
        private readonly int _timeout;
        private readonly NetAddress _proxyAddr;
        int _done = 0;
        NetAddress _masterGateway;
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

        public async ValueTask<NetAddress> GetMasterAsync()
        {            
            var totalCount = _gatewayAddrs.Length;

            foreach (var addr in _gatewayAddrs)
            {
                if (_status == 1)
                    break;

                tryConnect(addr, totalCount);

            }

            while (_status == 0)
                await Task.Delay(10);

            if (_status == 2)
                throw new MissMasterGatewayException("无法找到主网关");

            while (_status == 3)
                await Task.Delay(10);

            var ret = _masterGateway;
            _masterGateway = null;

            return ret;
        }



        async void tryConnect(NetAddress addr, int totalCount)
        {

            var client = await NetClientPool.CreateClientAsync(_proxyAddr, addr);
            try
            {
                client.ReadTimeout = _timeout;
                client.WriteServiceData(new GatewayCommand
                {
                    Type = (int)CommandType.FindMaster
                });
                var ret = await client.ReadServiceObjectAsync<InvokeResult<FindMasterResult>>();
                NetClientPool.AddClientToPool(client);

                if (ret.Success == true && _masterGateway == null)
                {
                    if (Interlocked.CompareExchange(ref _status, 3, 0) == 0)
                    {
                        SupportRemoteConnection = ret.Data != null && ret.Data.SupportRetmoteClientConnect;
                        _masterGateway = addr;

                        _status = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                client.Dispose();
            }
            finally
            {
                var ret = Interlocked.Increment(ref _done);

                if (_lastError != null && ret == totalCount)
                {
                    Interlocked.CompareExchange(ref _status, 2, 0);
                }
            }
        }

    }

}
