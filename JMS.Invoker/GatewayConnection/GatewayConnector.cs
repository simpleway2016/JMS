using JMS.Dtos;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.GatewayConnection
{
    internal class GatewayConnector : IDisposable,IMicroServiceProvider
    {
        bool _supportRemoteConnection;
        NetAddress _proxy;
        NetAddress _gatewayAddress;
        bool _disposed = false;
        ProxyClient _client;
        public NetAddress GatewayAddress => _gatewayAddress;
        ConcurrentDictionary<string, RegisterServiceRunningInfo> _allServices = new ConcurrentDictionary<string, RegisterServiceRunningInfo>();
        public GatewayConnector(NetAddress proxy, NetAddress gatewayAddress, bool supportRemoteConnection)
        {
            this._supportRemoteConnection = supportRemoteConnection;
            this._proxy = proxy;
            this._gatewayAddress = gatewayAddress;
            if (supportRemoteConnection)
            {
                keepConnect();
            }
        }

        async void keepConnect()
        {
            try
            {
                RegisterServiceRunningInfo curItem;
                using (_client = new ProxyClient(_proxy))
                {
                    await _client.ConnectAsync(_gatewayAddress);
                    await _client.WriteServiceDataAsync(new GatewayCommand
                    {
                        Type = CommandType.RemoteClientConnection
                    });

                    if (_disposed)
                        return;

                    var services = await _client.ReadServiceObjectAsync<RegisterServiceRunningInfo[]>();
                    _allServices.Clear();
                    foreach ( var item in services )
                    {
                        _allServices[item.ServiceId] = item;
                    }

                    while (!_disposed)
                    {
                        var ret = await _client.ReadServiceObjectAsync<GatewayConnectionResult>();
                        if(ret.Type == 1)
                        {
                            //新增
                            curItem = ret.Data.FromJson<RegisterServiceRunningInfo>();
                            _allServices[curItem.ServiceId] = curItem;
                        }
                        else if(ret.Type == 2)
                        {
                            //移除
                            _allServices.TryRemove(ret.Data, out curItem);
                        }
                    }
                }
            }
            catch
            {
                if (!_disposed)
                {
                    Task.Run(keepConnect);
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _client?.Dispose();
            _client = null;
           
        }

        public RegisterServiceRunningInfo GetServiceLocation(string serviceName,bool isGatewayProxy)
        {
            if(_supportRemoteConnection == false)
            {
                throw new NotImplementedException();
            }

            var matchServices = _allServices.Where(m => m.Value.ServiceList.Any(n => n.Name == serviceName && (n.AllowGatewayProxy || isGatewayProxy == false))
            && m.Value.MaxThread > 0
            && (m.Value.MaxRequestCount == 0 || m.Value.PerformanceInfo.RequestQuantity < m.Value.MaxRequestCount)
            ).Select(m=>m.Value);

            IEnumerable<RegisterServiceRunningInfo> services = null;

            //先查找cpu使用率低于70%的
            services = matchServices.Where(m => m.PerformanceInfo.CpuUsage < 70);


            if (services.Count() == 0)
                services = matchServices;

            //查找一个客户占用比较低的机器
            var item = services.OrderBy(m => m.PerformanceInfo.RequestQuantity / m.MaxThread).FirstOrDefault();
            if (item == null)
                return null;

            Interlocked.Increment(ref item.PerformanceInfo.RequestQuantity);

            return item;
        }

        class GatewayConnectionResult
        {
            public int Type { get; set; }
            public string Data { get; set; }
        }
    }

    
}
