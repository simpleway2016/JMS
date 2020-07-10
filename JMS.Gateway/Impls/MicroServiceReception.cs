using JMS.Dtos;
using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Utilities.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Impls
{
    class MicroServiceReception: IMicroServiceReception
    {
        ILogger<MicroServiceReception> _Logger;
        Gateway _Gateway;
        NetClient NetClient;
        public RegisterServiceInfo ServiceInfo { get; set; }

        IServiceProviderAllocator _ServiceProviderAllocator;
        LockKeyManager _lockKeyManager;
        GatewayRefereeClient _gatewayReferee;
        public MicroServiceReception(ILogger<MicroServiceReception> logger,
            Gateway gateway, 
            LockKeyManager lockKeyManager,
            GatewayRefereeClient gatewayReferee,
            IServiceProviderAllocator serviceProviderAllocator)
        {           
            _Gateway = gateway;
            _ServiceProviderAllocator = serviceProviderAllocator;
               _Logger = logger;
            _lockKeyManager = lockKeyManager;
            _gatewayReferee = gatewayReferee;
        }
        public void HealthyCheck( NetClient netclient, GatewayCommand registerCmd)
        {
            this.NetClient = netclient;
            ServiceInfo = registerCmd.Content.FromJson<RegisterServiceInfo>();

            _gatewayReferee.AddMicroService(ServiceInfo);

            ServiceInfo.Host = ((IPEndPoint)NetClient.Socket.RemoteEndPoint).Address.ToString();
            NetClient.WriteServiceData(new InvokeResult{ 
                Success = true
            });
            lock(_Gateway.OnlineMicroServices)
            {
                _Gateway.OnlineMicroServices.Add(this);                
            }
            SystemEventCenter.OnMicroServiceOnline(this.ServiceInfo);

            Task.Run(() => {
                _ServiceProviderAllocator.ServiceInfoChanged(_Gateway.GetAllServiceProviders());
            });
            _Logger?.LogInformation($"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}注册");

            checkState();
        }

        void disconnect()
        {
            lock (_Gateway.OnlineMicroServices)
            {
                _Gateway.OnlineMicroServices.Remove(this);
            }
            SystemEventCenter.OnMicroServiceOnffline(this.ServiceInfo);

            Task.Run(() => {
                _ServiceProviderAllocator.ServiceInfoChanged(_Gateway.GetAllServiceProviders());
            });
        }
        public void Close()
        {
            NetClient?.Dispose();
        }
        void checkState()
        {
            NetClient.ReadTimeout = 30000;
            while(true)
            {
                try
                {
                    var command = NetClient.ReadServiceObject<GatewayCommand>();
                    NetClient.WriteServiceData(new InvokeResult
                    {
                        Success = true
                    });

                    if (command.Type == CommandType.ReportClientConnectQuantity)
                    {                       
                        //微服务向我报告当前它的请求连接数
                        //_Logger?.LogDebug($"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port} 当前连接数：{command.Content}");
                        Task.Run(() =>
                        {
                            try
                            {
                                var hardwareInfo = command.Content.FromJson<PerformanceInfo>();
                                _ServiceProviderAllocator.SetServicePerformanceInfo(this.ServiceInfo, hardwareInfo);
                            }
                            catch
                            {
                            }
                        });                       
                    }
                }
                catch(SocketException)
                {
                    _Logger?.LogInformation( $"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}断开");

                    disconnect();
                    return;
                }
                catch (Exception ex)
                {
                    _Logger?.LogInformation(ex, $"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}断开");
                  
                    disconnect();
                    return;
                }
            }
        }
    }
}
