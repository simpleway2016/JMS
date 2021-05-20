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

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using JMS.Net;
using JMS.Interfaces.Hardware;

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
        ICpuInfo _cpuInfo;
        public GatewayConnector(MicroServiceHost microServiceHost,
            ICpuInfo cpuInfo,
            SSLConfiguration sSLConfiguration,
            ILogger<GatewayConnector> logger,IKeyLocker keyLocker)
        {
            _microServiceHost = microServiceHost;
            _logger = logger;
            _keyLocker = keyLocker;
            _cpuInfo = cpuInfo;
            _SSLConfiguration = sSLConfiguration;
        }
       
        public NetClient CreateClient(NetAddress addr)
        {
            return new GatewayClient(addr, _SSLConfiguration);
        }

        public void ConnectAsync()
        {
            new Thread(connect).Start();
        }
        public void OnServiceNameListChanged()
        {
            while (!_ready)
                Thread.Sleep(1000);

            if (_microServiceHost.MasterGatewayAddress == null)
                return;

            using (var client = CreateClient(_microServiceHost.MasterGatewayAddress))
            {
                client.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.ServiceNameListChanged,
                    Content = new RegisterServiceInfo
                    {
                        ServiceNames = _microServiceHost.ServiceNames.Where(m=>m.Value.Enable).Select(m=>m.Key).ToArray(),
                        Port = _microServiceHost.ServicePort,
                        MaxThread = Environment.ProcessorCount,
                        ServiceId = _microServiceHost.Id,
                        Description = _microServiceHost.Description,
                        ClientCheckCode = _microServiceHost.ClientCheckCode,
                    }.ToJsonString()
                });
                client.ReadServiceObject<InvokeResult>();
            }
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

            bool logError = true;
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

                            if(logError)
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
                logError = false;
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
                        ServiceNames = _microServiceHost.ServiceNames.Where(m => m.Value.Enable).Select(m => m.Key).ToArray(),
                        Port = _microServiceHost.ServicePort,
                        ServiceAddress = _microServiceHost.ServiceAddress == null?null: _microServiceHost.ServiceAddress.Address,
                        MaxThread = Environment.ProcessorCount,
                        ServiceId = _microServiceHost.Id,
                        Description = _microServiceHost.Description,
                        ClientCheckCode = _microServiceHost.ClientCheckCode
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
                        Content = _keyLocker.GetLockedKeys().ToJsonString()
                    });
                    var cb = client.ReadServiceObject<InvokeResult<string[]>>();
                    if (cb.Data.Length > 0)
                    {
                        _logger?.LogInformation("以下key锁失败,{0}", cb.Data.ToJsonString());
                        foreach( var key in cb.Data )
                        {
                            _keyLocker.RemoveKeyFromLocal(key);
                        }
                    }
                }

                //保持心跳，并且定期发送ClientConnected
                _client.KeepHeartBeating(() => {
                    return new GatewayCommand
                    {
                        Type = CommandType.ReportClientConnectQuantity,
                        Content = new PerformanceInfo { 
                            RequestQuantity = _microServiceHost.ClientConnected , 
                            CpuUsage = _cpuInfo.GetCpuUsage() 
                        }.ToJsonString()
                    };
                });
                _client.Dispose();
                _client = null;

                _logger?.LogError("和网关连接断开");
                if( _microServiceHost.AutoExitProcess )
                {
                    _logger?.LogInformation("和网关连接断开，准备自动关闭进程");
                    var handler = _microServiceHost.ServiceProvider.GetService<ProcessExitHandler>();
                    if(handler != null)
                    {
                        handler.OnProcessExit();
                    }
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                    return;
                }
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
            if(_client != null && _client.Socket != null)
            {
                _client.Socket.Close();
                _logger?.LogInformation("Socket closed");
                _client.Dispose();
            }            
        }


    }
}
