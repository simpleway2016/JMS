using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks.Sources;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using JMS.Dtos;

namespace JMS
{
    class FindMasterGatewayTask : IValueTaskSource<NetAddress>
    {
        int _timeout;
        NetAddress _proxyAddr;
        X509Certificate2 _gatewayCert;
        NetAddress[] _gatewayAddrs;
        ValueTaskSourceStatus _status = ValueTaskSourceStatus.Pending;
        NetAddress _masterGateway;
        int _done = 0;
        public FindMasterGatewayTask(NetAddress[] gatewayAddrs, int timeout, NetAddress proxyAddr, X509Certificate2 gatewayCert)
        {
            this._timeout = timeout;
            this._proxyAddr = proxyAddr;
            this._gatewayCert = gatewayCert;
            this._gatewayAddrs = gatewayAddrs;

        }

        public NetAddress GetResult(short token)
        {
            if(_status == ValueTaskSourceStatus.Faulted)
            {
                throw new MissMasterGatewayException("无法找到主网关");
            }
            return _masterGateway;
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return _status;
        }

        async void checkGateway(NetAddress addr,Action<object> continuation,object state)
        {
            try
            {
                using (var client = new ProxyClient(_proxyAddr, _gatewayCert))
                {
                    await client.ConnectAsync(addr);
                    client.ReadTimeout = _timeout;
                    client.WriteServiceData(new GatewayCommand
                    {
                        Type = CommandType.FindMaster
                    });
                    var ret = await client.ReadServiceObjectAsync<InvokeResult>();
                    if (ret.Success == true && _masterGateway == null)
                    {
                        _masterGateway = addr;
                        _status = ValueTaskSourceStatus.Succeeded;
                        continuation(state);
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                Interlocked.Increment(ref _done);
                if(_done == _gatewayAddrs.Length && _status == ValueTaskSourceStatus.Pending)
                {
                    _status = ValueTaskSourceStatus.Faulted;
                    continuation(state);
                }
            }
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            for(int i = 0; i < _gatewayAddrs.Length; i++)
            {
                checkGateway(_gatewayAddrs[i], continuation, state);
            }
        }
    }
}
