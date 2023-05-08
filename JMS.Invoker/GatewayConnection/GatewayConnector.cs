using JMS.Dtos;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
                        else if (ret.Type == 3)
                        {
                            //更新
                            curItem = ret.Data.FromJson<RegisterServiceRunningInfo>();
                            if (_allServices.TryGetValue(curItem.ServiceId, out RegisterServiceRunningInfo exist))
                            {
                                _allServices.TryUpdate(curItem.ServiceId, curItem, exist);
                            }
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

        async Task<ClientServiceDetail> GetServiceLocationInGateway(IRemoteClient remoteClient, string serviceName)
        {
            //获取服务地址
            var netclient = await NetClientPool.CreateClientAsync(_proxy, _gatewayAddress);
            netclient.ReadTimeout = 8000;
            try
            {
                await netclient.WriteServiceDataAsync(new GatewayCommand()
                {
                    Type = CommandType.GetServiceProvider,
                    Header = remoteClient.GetCommandHeader(),
                    Content = new GetServiceProviderRequest
                    {
                        ServiceName = serviceName
                    }.ToJsonString()
                });
                var serviceLocation = await netclient.ReadServiceObjectAsync<ClientServiceDetail>();

                if (serviceLocation.ServiceAddress == "not master")
                    throw new MissMasterGatewayException("");

                if (serviceLocation.Port == 0 && string.IsNullOrEmpty(serviceLocation.ServiceAddress))
                {
                    //网关没有这个服务
                    return null;
                }

                NetClientPool.AddClientToPool(netclient);
                return serviceLocation;
            }
            catch (SocketException ex)
            {
                netclient.Dispose();
                throw new MissMasterGatewayException(ex.Message);
            }
            catch (Exception)
            {
                netclient.Dispose();
                throw;
            }
        }

        public async Task<ClientServiceDetail> GetServiceLocation(IRemoteClient remoteClient, string serviceName)
        {
            if(_supportRemoteConnection == false)
            {
                return await GetServiceLocationInGateway(remoteClient,serviceName);
            }

            var matchServices = _allServices.Where(m => m.Value.ServiceList.Any(n => n.Name == serviceName)
            && m.Value.MaxThread > 0
            && (m.Value.MaxRequestCount == 0 || m.Value.PerformanceInfo.RequestQuantity < m.Value.MaxRequestCount)
            ).Select(m=>m.Value);

            if(matchServices.Count() == 0)
            {
                return await GetServiceLocationInGateway(remoteClient,serviceName);

            }

            IEnumerable<RegisterServiceRunningInfo> services = null;

            //先查找cpu使用率低于70%的
            services = matchServices.Where(m => m.PerformanceInfo.CpuUsage < 70);


            if (services.Count() == 0)
                services = matchServices;

            //查找一个客户占用比较低的机器
            var item = services.OrderBy(m => m.PerformanceInfo.RequestQuantity / m.MaxThread).FirstOrDefault();
            if (item == null)
            {
                return null;
            }

            Interlocked.Increment(ref item.PerformanceInfo.RequestQuantity);

            return new ClientServiceDetail(item.ServiceList.FirstOrDefault(m=>m.Name == serviceName) , item);
        }

        class GatewayConnectionResult
        {
            public int Type { get; set; }
            public string Data { get; set; }
        }
    }

    
}
