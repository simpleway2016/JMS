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
    class GatewayConnector : IGatewayConnector
    {
        /// <summary>
        /// 在网关处的id
        /// </summary>
        internal int ServiceId;
        Way.Lib.NetStream _client;
        ILogger<GatewayConnector> _logger;
        MicroServiceProvider _microServiceProvider;
        bool _manualDisconnected;
        bool _ready;
        public GatewayConnector(MicroServiceProvider microServiceProvider,ILogger<GatewayConnector> logger)
        {
            _microServiceProvider = microServiceProvider;
            _logger = logger;
            new Thread(sendConnectQuantity).Start();           
        }
        public void ConnectAsync()
        {
            new Thread(connect).Start();
        }
        void connect()
        {
            try
            {
                _ready = false;
                _client?.Dispose();

                _client = new Way.Lib.NetStream(_microServiceProvider.GatewayAddress, _microServiceProvider.GatewayPort);
#if DEBUG
                _client.ReadTimeout = 0;
#else
                _client.ReadTimeout = 20000;
#endif

                GatewayCommand cmd = new GatewayCommand()
                {
                    Type = CommandType.Register,
                    Content = new RegisterServiceInfo
                    {
                        ServiceNames = _microServiceProvider.ServiceNames.Keys.ToArray(),
                        Port = _microServiceProvider.ServicePort,
                        MaxThread = Environment.ProcessorCount,
                        ServiceId = this.ServiceId
                    }.ToJsonString()
                };
                _client.WriteServiceData(cmd);
                var ret = _client.ReadServiceObject<InvokeResult>();
                this.ServiceId = Convert.ToInt32(ret.Data);

                _client.ReadTimeout = 0;
                _ready = true;
                _logger?.LogInformation("和网关连接成功,网关ip：{0} 网关端口：{1}", _microServiceProvider.GatewayAddress, _microServiceProvider.GatewayPort);

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
                            Content = _microServiceProvider.ClientConnected.ToString()
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
