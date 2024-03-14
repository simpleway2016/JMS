﻿using JMS.Dtos;
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
using JMS.Infrastructures.Hardware;
using JMS.Controllers;

namespace JMS
{
    /// <summary>
    /// 连接主网关，和维持主网关的心跳
    /// </summary>
    class GatewayConnector : IGatewayConnector
    {
        IConnectionCounter _connectionCounter;
        ControllerFactory _controllerFactory;
        NetClient _client;
        ILogger<GatewayConnector> _logger;
        MicroServiceHost _microServiceHost;
        bool _manualDisconnected;
        bool _ready;
        IKeyLocker _keyLocker;
        SSLConfiguration _SSLConfiguration;
        ICpuInfo _cpuInfo;
        public event EventHandler<System.Version> ConnectCompleted;
        string _singletonErrorMsg = "相同的服务已经在运行，连接等待中...";

        public DateTime? DisconnectTime { get; private set; }
        public GatewayConnector(MicroServiceHost microServiceHost,
            ICpuInfo cpuInfo,
            SSLConfiguration sSLConfiguration,
            ControllerFactory controllerFactory,
            IConnectionCounter connectionCounter,
            ILogger<GatewayConnector> logger, IKeyLocker keyLocker)
        {
            this._connectionCounter = connectionCounter;
            this._controllerFactory = controllerFactory;
            _microServiceHost = microServiceHost;
            _logger = logger;
            _keyLocker = keyLocker;
            _cpuInfo = cpuInfo;
            _SSLConfiguration = sSLConfiguration;
        }

        public NetClient CreateClient(NetAddress addr)
        {
            var client = new CertClient();
            client.Connect(addr);
            return client;
        }

        /// <summary>
        /// 检查事务是否已成功
        /// </summary>
        /// <param name="tranid"></param>
        /// <returns></returns>
        public bool CheckTransaction(string tranid)
        {
            using (var netclient = this.CreateClient(_microServiceHost.MasterGatewayAddress))
            {

                netclient.WriteServiceData(new GatewayCommand
                {
                    Type = (int)CommandType.GetTransactionStatus,
                    Content = tranid
                });

                var ret = netclient.ReadServiceObject<InvokeResult>();
                return ret.Success;
            }
        }

        /// <summary>
        /// 检查事务是否已成功
        /// </summary>
        /// <param name="tranid"></param>
        /// <returns></returns>
        public async Task<bool> CheckTransactionAsync(string tranid)
        {
            using (var netclient = this.CreateClient(_microServiceHost.MasterGatewayAddress))
            {

                netclient.WriteServiceData(new GatewayCommand
                {
                    Type = (int)CommandType.GetTransactionStatus,
                    Content = tranid
                });

                var ret = await netclient.ReadServiceObjectAsync<InvokeResult>();
                return ret.Success;
            }
        }

        public void ConnectAsync()
        {
            new Thread(connect).Start();
        }
        public void OnServiceInfoChanged()
        {
            while (!_ready)
                Thread.Sleep(1000);

            if (_microServiceHost.MasterGatewayAddress == null)
                return;

            var controllers = _controllerFactory.GetAllControllers();

            using (var client = CreateClient(_microServiceHost.MasterGatewayAddress))
            {
                client.WriteServiceData(new GatewayCommand()
                {
                    Type = (int)CommandType.ServiceNameListChanged,
                    Content = new RegisterServiceInfo
                    {
                        ServiceList = controllers.Where(m => m.Enable).Select(m => m.Service).ToArray(),
                        Port = (_microServiceHost.ServiceAddress == null || _microServiceHost.ServiceAddress.Port == 0) ? _microServiceHost.ServicePort : _microServiceHost.ServiceAddress.Port,
                        MaxThread = Environment.ProcessorCount,
                        ServiceId = _microServiceHost.Id,
                        ClientCheckCodeFile = _microServiceHost.ClientCheckCodeFile,
                        SingletonService = _microServiceHost.SingletonService,
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
            if (_microServiceHost.AllGatewayAddresses.Length == 1)
            {
                _microServiceHost.MasterGatewayAddress = _microServiceHost.AllGatewayAddresses[0];
                return;
            }

            _microServiceHost.MasterGatewayAddress = null;



            bool logError = true;
            while (_microServiceHost.MasterGatewayAddress == null)
            {
                ManualResetEvent waitObj = new ManualResetEvent(false);
                Task.Run(() =>
                {
                    bool exit = false;
                    Parallel.For(0, _microServiceHost.AllGatewayAddresses.Length, i =>
                    {
                        var addr = _microServiceHost.AllGatewayAddresses[i];
                        try
                        {
                            using (var client = CreateClient(addr))
                            {
                                client.WriteServiceData(new GatewayCommand
                                {
                                    Type = (int)CommandType.FindMaster
                                });
                                var ret = client.ReadServiceObject<InvokeResult>();
                                if (ret.Success == true && _microServiceHost.MasterGatewayAddress == null)
                                {
                                    _microServiceHost.MasterGatewayAddress = addr;
                                    waitObj.Set();
                                    exit = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (logError)
                                _logger?.LogError(ex, "验证网关{0}:{1}报错", addr.Address, addr.Port);
                        }
                    });

                    if (exit)
                        return;

                    waitObj.Set();
                });

                waitObj.WaitOne();
                waitObj.Dispose();

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

                if (_microServiceHost.SingletonService)
                {
                    try
                    {
                        _client = CreateClient(_microServiceHost.MasterGatewayAddress);
                        _client.WriteServiceData(new GatewayCommand()
                        {
                            Type = (int)CommandType.CheckSupportSingletonService
                        });
                        if (_client.ReadServiceObject<InvokeResult>().Success == false)
                        {
                            throw new Exception("网关不支持SingletonService，请更新网关程序");
                        }
                    }
                    catch
                    {
                        throw new Exception("网关不支持SingletonService，请更新网关程序");
                    }
                    finally
                    {
                        _client.Dispose();
                    }
                }

                _client = CreateClient(_microServiceHost.MasterGatewayAddress);

                DisconnectTime = null;
                var controllers = _controllerFactory.GetAllControllers();

                _client.WriteServiceData(new GatewayCommand()
                {
                    Type = (int)CommandType.RegisterSerivce,
                    Content = new RegisterServiceInfo
                    {
                        ServiceList = controllers.Where(m => m.Enable).Select(m => m.Service).ToArray(),
                        Port = (_microServiceHost.ServiceAddress == null || _microServiceHost.ServiceAddress.Port == 0) ? _microServiceHost.ServicePort : _microServiceHost.ServiceAddress.Port,
                        ServiceAddress = _microServiceHost.ServiceAddress == null ? null : _microServiceHost.ServiceAddress.Address,
                        MaxThread = Environment.ProcessorCount,
                        ServiceId = _microServiceHost.Id,
                        MaxRequestCount = _microServiceHost.MaxRequestCount,
                        ClientCheckCodeFile = _microServiceHost.ClientCheckCodeFile,
                        SingletonService = _microServiceHost.SingletonService,
                        Properties = new Dictionary<string, string>(){ { "EngineVersion", typeof(GatewayConnector).Assembly.GetName().Version.ToString() } },
                        UseSsl = (_SSLConfiguration != null && _SSLConfiguration.ServerCertificate != null)
                    }.ToJsonString()
                });
                var ret = _client.ReadServiceObject<InvokeResult<string>>();
                if (ret.Success == false)
                {
                    _client.Dispose();
                    _client = null;

                    if (ret.Error == "not allow")
                        throw new Exception("网关不允许当前ip作为微服务");
                    else if (ret.Error == "SingletonService")
                    {
                        if (_singletonErrorMsg != null)
                        {
                            _logger?.LogInformation(_singletonErrorMsg);
                            _singletonErrorMsg = null;
                        }
                        Thread.Sleep(1000);
                        this.ConnectAsync();
                        return;
                    }
                    else
                        throw new Exception("网关不允许连接");
                }

                if (ret.Data == null)
                {
                    throw new Exception("网关版本太旧，服务无法正常注册，请更新网关");

                }


                _ready = true;
                _logger?.LogInformation("和网关连接成功,网关ip：{0} 网关端口：{1} 网关版本：{2}", _microServiceHost.MasterGatewayAddress.Address, _microServiceHost.MasterGatewayAddress.Port, ret.Data);

               
                //上传已经lock的key
                if (_keyLocker.GetLockedKeys().Length > 0)
                {
                    using (var client = CreateClient(_microServiceHost.MasterGatewayAddress))
                    {
                        client.WriteServiceData(new GatewayCommand
                        {
                            Type = (int)CommandType.UploadLockKeys,
                            Header = new Dictionary<string, string> {
                                    { "ServiceId",_microServiceHost.Id}
                                },
                            Content = _keyLocker.GetLockedKeys().ToJsonString()
                        });
                        var cb = client.ReadServiceObject<InvokeResult<string[]>>();
                        if (cb.Data.Length > 0)
                        {
                            _logger?.LogInformation("以下key锁失败,{0}", cb.Data.ToJsonString());
                            foreach (var key in cb.Data)
                            {
                                _keyLocker.RemoveKeyFromLocal(key);
                            }
                        }
                    }
                }

                ConnectCompleted?.Invoke(this, new Version(ret.Data));

                //保持心跳，并且定期发送ClientConnected
                _client.KeepHeartBeating(() =>
                {
                    return new GatewayCommand
                    {
                        Type = (int)CommandType.ReportClientConnectQuantity,
                        Content = new PerformanceInfo
                        {
                            RequestQuantity = _connectionCounter.ConnectionCount,
                            CpuUsage = _cpuInfo.GetCpuUsage()
                        }.ToJsonString()
                    };
                });
                _client.Dispose();
                _client = null;
                DisconnectTime = DateTime.Now;

                _logger?.LogError("和网关连接断开");
                if (_microServiceHost.AutoExitProcess || _microServiceHost.SingletonService)
                {
                    _logger?.LogInformation("和网关连接断开，准备自动关闭进程");
                    var handler = _microServiceHost.ServiceProvider.GetService<ProcessExitHandler>();
                    if (handler != null)
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
                    DisconnectTime = DateTime.Now;
                    Thread.Sleep(2000);
                    this.ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
                if (!_manualDisconnected)
                {
                    DisconnectTime = DateTime.Now;
                    Thread.Sleep(2000);
                    this.ConnectAsync();
                }
            }

        }

        public void DisconnectGateway()
        {
            _manualDisconnected = true;
            if (_client != null && _client.Socket != null)
            {
                _client.Socket.Close();
                _logger?.LogInformation("Socket closed");
                _client.Dispose();
            }
        }


    }
}
