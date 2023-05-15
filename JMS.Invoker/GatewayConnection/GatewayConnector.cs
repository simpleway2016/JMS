using JMS.Dtos;
using Microsoft.Extensions.Logging;
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
    internal class GatewayConnector : IDisposable, IMicroServiceProvider
    {
        ILogger _logger;
        bool _supportRemoteConnection;
        NetAddress _proxy;
        NetAddress _gatewayAddress;
        bool _disposed = false;
        ProxyClient _client;
        public NetAddress GatewayAddress => _gatewayAddress;
        List<RegisterServiceRunningInfo> _allServices = new List<RegisterServiceRunningInfo>(1024);
        public GatewayConnector(NetAddress proxy, NetAddress gatewayAddress, bool supportRemoteConnection, ILogger logger)
        {
            this._logger = logger;
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
                _logger?.LogDebug($"正在连接网关:{_gatewayAddress} {(_proxy!=null?"代理:":"")}{_proxy}");
                RegisterServiceRunningInfo curItem;
                using (_client = new ProxyClient(_proxy))
                {
                    await _client.ConnectAsync(_gatewayAddress);
                    _client.KeepAlive = true;
                    await _client.WriteServiceDataAsync(new GatewayCommand
                    {
                        Type = CommandType.FindMaster
                    });
                    var findMasterRet = await _client.ReadServiceObjectAsync<InvokeResult>();
                    if (findMasterRet.Success == false)
                    {
                        _logger?.LogDebug($"{_gatewayAddress}不是主网关，放弃连接");
                        return;
                    }

                    _client.KeepAlive = false;
                    await _client.WriteServiceDataAsync(new GatewayCommand
                    {
                        Type = CommandType.RemoteClientConnection
                    });

                    if (_disposed)
                        return;

                    _logger?.LogDebug($"成功连接{_gatewayAddress}");
                    var services = await _client.ReadServiceObjectAsync<RegisterServiceRunningInfo[]>();                    
                    lock (_allServices)
                    {
                        _allServices.Clear();
                        _allServices.AddRange(services);
                    }

                    while (!_disposed)
                    {
                        var ret = await _client.ReadServiceObjectAsync<GatewayConnectionResult>();
                        if (ret.Type == 1)
                        {
                            //新增
                            curItem = ret.Data.FromJson<RegisterServiceRunningInfo>();
                            lock (_allServices)
                            {
                                _allServices.Add(curItem);
                            }
                        }
                        else if (ret.Type == 2)
                        {
                            //移除
                            lock (_allServices)
                            {
                                for (int i = 0; i < _allServices.Count; i++)
                                {
                                    try
                                    {
                                        var item = _allServices[i];
                                        if (item.ServiceId == ret.Data)
                                        {
                                            _allServices.RemoveAt(i);
                                            break;
                                        }
                                    }
                                    catch
                                    {
                                        i--;
                                    }
                                }
                            }
                        }
                        else if (ret.Type == 3)
                        {
                            //更新
                            curItem = ret.Data.FromJson<RegisterServiceRunningInfo>();

                            for (int i = 0; i < _allServices.Count; i++)
                            {
                                try
                                {
                                    var exist = _allServices[i];
                                    if (exist.ServiceId == curItem.ServiceId)
                                    {
                                        exist.ServiceList = curItem.ServiceList;
                                        exist.PerformanceInfo = curItem.PerformanceInfo;
                                        exist.ServiceAddress = curItem.ServiceAddress;
                                        exist.Port = curItem.Port;
                                        exist.UseSsl = curItem.UseSsl;
                                        break;
                                    }
                                }
                                catch
                                {
                                    i--;
                                }
                            }

                        }
                    }
                }
            }
            catch
            {
                _logger?.LogDebug($"与网关断开{_gatewayAddress}");
                if (!_disposed)
                {
                    lock (_allServices)
                    {
                        _allServices.Clear();
                    }
                    await Task.Delay(2000);
                    keepConnect();
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _client?.Dispose();
            _client = null;

        }

        async Task<ClientServiceDetail> GetServiceLocationInGatewayAsync(IRemoteClient remoteClient, string serviceName)
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

        ClientServiceDetail GetServiceLocationInGateway(IRemoteClient remoteClient, string serviceName)
        {
            //获取服务地址
            var netclient = NetClientPool.CreateClient(_proxy, _gatewayAddress);
            netclient.ReadTimeout = 8000;
            try
            {
                netclient.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.GetServiceProvider,
                    Header = remoteClient.GetCommandHeader(),
                    Content = new GetServiceProviderRequest
                    {
                        ServiceName = serviceName
                    }.ToJsonString()
                });
                var serviceLocation = netclient.ReadServiceObject<ClientServiceDetail>();

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

        public async Task<ClientServiceDetail> GetServiceLocationAsync(IRemoteClient remoteClient, string serviceName)
        {
            if (_supportRemoteConnection == false)
            {
                return await GetServiceLocationInGatewayAsync(remoteClient, serviceName);
            }

            RegisterServiceRunningInfo[] matchServices;
            while (true)
            {
                try
                {
                    matchServices = _allServices.Where(m => m.ServiceList.Any(n => n.Name == serviceName)
                    && m.MaxThread > 0
                    && (m.MaxRequestCount == 0 || m.PerformanceInfo.RequestQuantity < m.MaxRequestCount)
                    ).ToArray();

                    break;
                }
                catch
                {
                    Thread.Sleep(10);
                }
            }

            if (matchServices.Length == 0)
            {
                return await GetServiceLocationInGatewayAsync(remoteClient, serviceName);

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

            return new ClientServiceDetail(item.ServiceList.FirstOrDefault(m => m.Name == serviceName), item);
        }

        public ClientServiceDetail GetServiceLocation(IRemoteClient remoteClient, string serviceName)
        {
            if (_supportRemoteConnection == false)
            {
                return GetServiceLocationInGateway(remoteClient, serviceName);
            }

            RegisterServiceRunningInfo[] matchServices;
            while (true)
            {
                try
                {
                    matchServices = _allServices.Where(m => m.ServiceList.Any(n => n.Name == serviceName)
                    && m.MaxThread > 0
                    && (m.MaxRequestCount == 0 || m.PerformanceInfo.RequestQuantity < m.MaxRequestCount)
                    ).ToArray();

                    break;
                }
                catch
                {
                    Thread.Sleep(10);
                }
            }

            if (matchServices.Length == 0)
            {
                return GetServiceLocationInGateway(remoteClient, serviceName);

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

            return new ClientServiceDetail(item.ServiceList.FirstOrDefault(m => m.Name == serviceName), item);
        }

        class GatewayConnectionResult
        {
            public int Type { get; set; }
            public string Data { get; set; }
        }
    }


}
