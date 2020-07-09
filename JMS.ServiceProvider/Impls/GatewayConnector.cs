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
using Microsoft.Extensions.DependencyInjection;
using JMS.Common.Dtos;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using JMS.Net;

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
        SSLConfiguration _SSLConfiguration;
        public GatewayConnector(MicroServiceHost microServiceHost,ILogger<GatewayConnector> logger,IKeyLocker keyLocker)
        {
            _microServiceHost = microServiceHost;
            _logger = logger;
            _keyLocker = keyLocker;
            _SSLConfiguration = microServiceHost.ServiceProvider.GetService<SSLConfiguration>();
        }
       
        public NetClient CreateClient(NetAddress addr)
        {
            return new GatewayClient(addr, _SSLConfiguration);
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
                int errCount = 0;
                ManualResetEvent waitobj = new ManualResetEvent(false);
                for (int i = 0; i < _microServiceHost.AllGatewayAddresses.Length; i ++)
                {
                    var addr = _microServiceHost.AllGatewayAddresses[i];
                    Task.Run(() => {
                        try
                        {
                            using (var client = CreateClient(addr))
                            {
                                client.WriteServiceData(new GatewayCommand
                                {
                                    Type = CommandType.FindMaster
                                });
                                var ret = client.ReadServiceObject<InvokeResult>();
                                if (ret.Success == true && _microServiceHost.MasterGatewayAddress == null)
                                {
                                    _microServiceHost.MasterGatewayAddress = addr;
                                    waitobj.Set();
                                }
                                else
                                {
                                    Interlocked.Increment(ref errCount);
                                    if (errCount == _microServiceHost.AllGatewayAddresses.Length)
                                        waitobj.Set();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref errCount);
                            if (errCount == _microServiceHost.AllGatewayAddresses.Length)
                                waitobj.Set();

                            _logger?.LogError(ex, "验证网关{0}:{1}报错", addr.Address, addr.Port);
                        }
                    });
                }
                waitobj.WaitOne();
                waitobj.Dispose();

                if (_microServiceHost.MasterGatewayAddress != null)
                {
                    _logger?.LogInformation("找到主网关{0}:{1}", _microServiceHost.MasterGatewayAddress.Address, _microServiceHost.MasterGatewayAddress.Port);
                    break;
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

                _client = CreateClient(_microServiceHost.MasterGatewayAddress);
                
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
                using (var client = CreateClient(_microServiceHost.MasterGatewayAddress))
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

                //保持心跳，并且定期发送ClientConnected
                _client.KeepHeartBeating(() => {
                    return new GatewayCommand
                    {
                        Type = CommandType.ReportClientConnectQuantity,
                        Content = _microServiceHost.ClientConnected.ToString()
                    };
                });

                _logger?.LogError("和网关连接断开");
                if (!_manualDisconnected)
                {
                    Thread.Sleep(2000);
                    this.ConnectAsync();
                }
            }
            catch (SocketException)
            {
                if (!_manualDisconnected)
                {
                    Thread.Sleep(2000);
                    this.ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
                if (!_manualDisconnected)
                {
                    Thread.Sleep(2000);
                    this.ConnectAsync();
                }
            }

        }

        public void DisconnectGateway()
        {
            _manualDisconnected = true;
            _client?.Dispose();
        }


    }
}
