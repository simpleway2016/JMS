using JMS.Dtos;
using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Impls
{
    /// <summary>
    /// 连接主网关，和维持主网关的心跳
    /// </summary>
    class GatewayConnector : IGatewayConnector
    {
        NetClient _client;
        ILogger<GatewayConnector> _logger;
        MicroServiceHost _microServiceHost;
        bool _manualDisconnected;
        bool _ready;
        IKeyLocker _keyLocker;
        public GatewayConnector(MicroServiceHost microServiceHost,ILogger<GatewayConnector> logger,IKeyLocker keyLocker)
        {
            _microServiceHost = microServiceHost;
            _logger = logger;
            _keyLocker = keyLocker;

            new Thread(sendConnectQuantity).Start();           
        }
        public void ConnectAsync()
        {
            new Thread(connect).Start();
        }

        /// <summary>
        /// 找出master网关
        /// </summary>
        void findMasterGateway()
        {
            if(_microServiceHost.AllGatewayAddresses.Length == 1)
            {
                _microServiceHost.MasterGatewayAddress = _microServiceHost.AllGatewayAddresses[0];
                return;
            }

            _microServiceHost.MasterGatewayAddress = null;
            while (_microServiceHost.MasterGatewayAddress == null)
            {
                Parallel.For(0, _microServiceHost.AllGatewayAddresses.Length, (index) =>
                {
                    var addr = _microServiceHost.AllGatewayAddresses[index];
                    try
                    {
                        using (var client = new NetClient(addr))
                        {
                            client.WriteServiceData(new GatewayCommand
                            {
                                Type = CommandType.FindMaster
                            });
                            var ret = client.ReadServiceObject<InvokeResult>();
                            if (ret.Success == true && _microServiceHost.MasterGatewayAddress == null)
                            {
                                _microServiceHost.MasterGatewayAddress = addr;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "验证网关{0}:{1}报错", addr.Address, addr.Port);
                    }

                });

                if(_microServiceHost.MasterGatewayAddress != null)
                {
                    _logger?.LogInformation("找到主网关{0}:{1}", _microServiceHost.MasterGatewayAddress.Address, _microServiceHost.MasterGatewayAddress.Port);
                }
                Thread.Sleep(1000);
            }
           
        }
        void connect()
        {
            try
            {
                _ready = false;
                _client?.Dispose();

                findMasterGateway();

                _client = new NetClient(_microServiceHost.MasterGatewayAddress.Address, _microServiceHost.MasterGatewayAddress.Port);
                
                _client.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.RegisterSerivce,
                    Content = new RegisterServiceInfo
                    {
                        ServiceNames = _microServiceHost.ServiceNames.Keys.ToArray(),
                        Port = _microServiceHost.ServicePort,
                        MaxThread = Environment.ProcessorCount,
                        ServiceId = _microServiceHost.Id
                    }.ToJsonString()
                });
                var ret = _client.ReadServiceObject<InvokeResult>();
                if(ret.Success == false)
                {
                    _client.Dispose();
                    throw new Exception("网关不允许当前ip作为微服务");
                }

                _ready = true;
                _logger?.LogInformation("和网关连接成功,网关ip：{0} 网关端口：{1}", _microServiceHost.MasterGatewayAddress.Address, _microServiceHost.MasterGatewayAddress.Port);


                //上传已经lock的key
                using (var client = new NetClient(_microServiceHost.MasterGatewayAddress))
                {
                    client.WriteServiceData(new GatewayCommand
                    {
                        Type = CommandType.UploadLockKeys,
                        Header = new Dictionary<string, string> {
                                    { "ServiceId",_microServiceHost.Id}
                                },
                        Content = _keyLocker.LockedKeys.ToJsonString()
                    });
                    client.ReadServiceObject<InvokeResult>();
                }

                new Thread(healthyCheck).Start();
            }
            catch (SocketException)
            {
                if (!_manualDisconnected)
                {
                    Thread.Sleep(2000);
                    connect();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
                if (!_manualDisconnected)
                {
                    Thread.Sleep(2000);
                    connect();
                }
            }

        }

        public void DisconnectGateway()
        {
            _manualDisconnected = true;
            _client?.Dispose();
        }


        /// <summary>
        /// 定时发送当前连接数
        /// </summary>
        void sendConnectQuantity()
        {
            while (!_manualDisconnected)
            {
                try
                {
                    Thread.Sleep(10000);
                    if(_ready)
                    {
                        _client.WriteServiceData(new GatewayCommand
                        {
                            Type = CommandType.ReportClientConnectQuantity,
                            Content = _microServiceHost.ClientConnected.ToString()
                        });
                    }
                    
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                }
            }
        }

        void healthyCheck()
        {
            while (!_manualDisconnected)
            {
                if(!_ready)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                try
                {
                    _client.ReadTimeout = 0;
                    var data = _client.ReadServiceData();
                }
                catch (SocketException)
                {
                    _client.Dispose();
                    _logger?.LogError("和网关连接断开");
                    if (!_manualDisconnected)
                    {
                        Thread.Sleep(2000);
                        connect();
                    }
                    return;
                }
                catch (Exception ex)
                {
                    _client.Dispose();
                    _logger?.LogError(ex, ex.Message);
                    if (!_manualDisconnected)
                    {
                        Thread.Sleep(2000);
                        connect();
                    }
                    return;
                }
            }
        }
    }
}
