using JMS.Dtos;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.GatewayConnection
{
    internal class MasterGatewayProvider
    {
        string _key;
        int _timeout;
        NetAddress _proxy;
        NetAddress[] _allGateways;

        GatewayConnector _master;
        static ConcurrentDictionary<string, MasterGatewayProvider> Providers = new ConcurrentDictionary<string, MasterGatewayProvider>();
        public static MasterGatewayProvider Create(NetAddress proxy, NetAddress[] gateWays, int timeout)
        {
            var key = string.Join(',', gateWays.Select(m => m.ToString()));

            var masterGatewayProvider = Providers.GetOrAdd(key,k=> new MasterGatewayProvider(proxy, gateWays, timeout, key));

            return masterGatewayProvider;
        }

        public MasterGatewayProvider(NetAddress proxy, NetAddress[] allGateways, int timeout, string key)
        {
            this._key = key;
            this._timeout = timeout;
            this._proxy = proxy;
            this._allGateways = allGateways;

        }

        public NetAddress GetMaster()
        {
            if (_allGateways == null)
                return null;

            if (_allGateways.Length == 1)
            {
                return _allGateways[0];
            }


            if(_master != null)
            {
                return _master.GatewayAddress;
            }

            NetAddress masterAddress = null;
            ManualResetEvent waitObj = new ManualResetEvent(false);
            bool supportRemoteConnection = false;

            Task.Run(() => {
                bool exit = false;
                Parallel.For(0, _allGateways.Length, i => {
                    var addr = _allGateways[i];

                    var client = NetClientPool.CreateClient(this._proxy, addr);
                    try
                    {
                        client.ReadTimeout = _timeout;
                        client.WriteServiceData(new GatewayCommand
                        {
                            Type = CommandType.FindMaster
                        });
                        var ret = client.ReadServiceObject<InvokeResult<FindMasterResult>>();
                        NetClientPool.AddClientToPool(client);

                        if (ret.Success == true && masterAddress == null)
                        {
                            supportRemoteConnection = ret.Data != null && ret.Data.SupportRetmoteClientConnect;
                            masterAddress = addr;
                            waitObj.Set();
                            exit = true;
                        }
                    }
                    catch
                    {
                        client.Dispose();
                    }
                });

                if (exit)
                    return;

                waitObj.Set();
            });

            waitObj.WaitOne();
            waitObj.Dispose();


            if (masterAddress == null)
                throw new MissMasterGatewayException("无法找到主网关");

            _master = new GatewayConnector(_proxy, masterAddress, supportRemoteConnection);

            return masterAddress;
        }

        public async Task<NetAddress> GetMasterAsync()
        {
            if (_allGateways == null)
                return null;

            if ( _allGateways.Length == 1)
            {
                return _allGateways[0];
            }

            if (_master != null)
            {
                return _master.GatewayAddress;
            }


            var task = new FindMasterGatewayTask(_allGateways, _timeout, _proxy);
            var masterAddress = await new ValueTask<NetAddress>(task, 0);
            _master = new GatewayConnector( _proxy, masterAddress, task.SupportRemoteConnection);


            return _master.GatewayAddress;
        }

        /// <summary>
        /// 忘记当前master
        /// </summary>
        public void RemoveMaster()
        {
            lock (this)
            {
                _master?.Dispose();
                _master = null;
            }
        }
    }

    class FindMasterResult
    {
        public bool SupportRetmoteClientConnect { get; set; }
        public string Version { get; set; }
    }
}
