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
    class GatewayConnector : IGatewayConnector
    {
        NetClient _client;
        ILogger<GatewayConnector> _logger;
        MicroServiceHost _microServiceHost;
        bool _manualDisconnected;
        bool _ready;
        public GatewayConnector(MicroServiceHost microServiceHost,ILogger<GatewayConnector> logger)
        {
            _microServiceHost = microServiceHost;
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

                _client = new NetClient(_microServiceHost.GatewayAddress, _microServiceHost.GatewayPort);

                GatewayCommand cmd = new GatewayCommand()
                {
                    Type = CommandType.Register,
                    Content = new RegisterServiceInfo
                    {
                        ServiceNames = _microServiceHost.ServiceNames.Keys.ToArray(),
                        Port = _microServiceHost.ServicePort,
                        MaxThread = Environment.ProcessorCount,
                        ServiceId = _microServiceHost.Id
                    }.ToJsonString()
                };
                _client.WriteServiceData(cmd);
                var ret = _client.ReadServiceObject<InvokeResult>();
                if(ret.Success == false)
                {
                    _client.Dispose();
                    throw new Exception("网关不允许当前ip作为微服务");
                }

                _ready = true;
                _logger?.LogInformation("和网关连接成功,网关ip：{0} 网关端口：{1}", _microServiceHost.GatewayAddress, _microServiceHost.GatewayPort);

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
