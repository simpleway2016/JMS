using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS
{
    public class GatewayConnector
    {
        System.Collections.Concurrent.ConcurrentQueue<string> _buffer;
        Way.Lib.NetStream _client;
        AutoResetEvent _event;
        public event EventHandler Disconnect;
        ILogger<GatewayConnector> _logger;
        MicroServiceProvider _microServiceProvider;
        bool _manualDisconnected = false;
        public GatewayConnector(ILogger<GatewayConnector> logger , MicroServiceProvider microServiceProvider)
        {
            _microServiceProvider = microServiceProvider;
               _logger = logger;
               _buffer = new System.Collections.Concurrent.ConcurrentQueue<string>();
            _event = new AutoResetEvent(false);
        }

        internal void DisconnectGateway()
        {
            _manualDisconnected = true;
               _client?.Dispose();
        }
        public void Connect(string gatewayAddress, int gatewayPort)
        {
            try
            {
                _client?.Dispose();

                _client = new Way.Lib.NetStream(gatewayAddress, gatewayPort);
                _client.ReadTimeout = 0;
                new Thread(read).Start();
            }
            catch(SocketException)
            {
                if (!_manualDisconnected && Disconnect != null)
                {
                    try
                    {
                        Disconnect(this, null);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {               
                _logger?.LogError(ex, ex.Message);
                if (!_manualDisconnected && Disconnect != null)
                {
                    try
                    {
                        Disconnect(this, null);
                    }
                    catch
                    {
                    }
                }
            }
           
        }

        public void Register(Dictionary<string, Type> ServiceNames,int servicePort)
        {
            if (_client == null)
                return;

            GatewayCommand cmd = new GatewayCommand()
            {
                Type = CommandType.Register,
                Content = new RegisterServiceInfo
                {
                    ServiceNames = ServiceNames.Keys.ToArray(),
                    Port = servicePort,
                    MaxThread = Environment.ProcessorCount
                }.ToJsonString()
            };
            _client.WriteServiceData(cmd);

            _event.WaitOne();
            if (_buffer.TryDequeue(out string data))
            {
                _logger?.LogInformation("成功连接网关");
                new Thread(sendConnectQuantity).Start();
                return;
            }
            else
                throw new Exception("网关没有回应数据");
        }

        /// <summary>
        /// 定时发送当前连接数
        /// </summary>
        void sendConnectQuantity()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(10000);
                    _client.WriteServiceData(new GatewayCommand { 
                        Type = CommandType.ReportClientConnectQuantity,
                        Content = _microServiceProvider.ClientConnected.ToString()
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                }
            }
        }

        void read()
        {
            while(true)
            {
                try
                {
                    var data =  _client.ReadServiceData();
                    _buffer.Enqueue(data);
                    _event.Set();
                }
                catch(SocketException)
                {
                    _client.Dispose();
                    _logger?.LogError("和网关连接断开");
                    if (!_manualDisconnected && Disconnect != null)
                    {
                        try
                        {
                            Disconnect(this, null);
                        }
                        catch
                        {
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    _client.Dispose();
                    _logger?.LogError(ex, ex.Message);
                    if (!_manualDisconnected && Disconnect != null)
                    {
                        try
                        {
                            Disconnect(this, null);
                        }
                        catch
                        {
                        }
                    }
                    return;
                }
            }
        }
    }
}
