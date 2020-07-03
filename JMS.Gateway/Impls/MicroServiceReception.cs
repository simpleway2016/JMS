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
        Way.Lib.NetStream NetClient;
        public RegisterServiceInfo ServiceInfo { get; set; }
        public int Id { get; private set; }
        static int Seed;
        IServiceProviderAllocator _ServiceProviderAllocator;
        LockKeyManager _lockKeyManager;
        public MicroServiceReception(ILogger<MicroServiceReception> logger,
            Gateway gateway, 
            LockKeyManager lockKeyManager,
            IServiceProviderAllocator serviceProviderAllocator)
        {           
            _Gateway = gateway;
            _ServiceProviderAllocator = serviceProviderAllocator;
               _Logger = logger;
            _lockKeyManager = lockKeyManager;
        }
        public void HealthyCheck( Way.Lib.NetStream netclient, GatewayCommand registerCmd)
        {
            this.NetClient = netclient;
            ServiceInfo = registerCmd.Content.FromJson<RegisterServiceInfo>();
            if(ServiceInfo.ServiceId > 0 && ServiceInfo.GatewayId == _Gateway.Id)
            {
                //这是一个曾经断开的微服务
                this.Id = ServiceInfo.ServiceId;
            }
            else
            {
                this.Id = Interlocked.Increment(ref Seed);
            }
            ServiceInfo.Host = ((IPEndPoint)NetClient.Socket.RemoteEndPoint).Address.ToString();
            NetClient.WriteServiceData(new InvokeResult{ 
                Data = new string[] { this.Id.ToString(), _Gateway.Id }
            });
            lock(_Gateway.OnlineMicroServices)
            {
                _Gateway.OnlineMicroServices.Add(this);                
            }
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
            Task.Run(() => {
                _ServiceProviderAllocator.ServiceInfoChanged(_Gateway.GetAllServiceProviders());
            });
        }

        void checkState()
        {
            while(true)
            {
                try
                {
                    var command = NetClient.ReadServiceObject<GatewayCommand>();
                    if (command.Type == CommandType.ReportClientConnectQuantity)
                    {
                        //微服务向我报告当前它的请求连接数
                        //_Logger?.LogDebug($"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port} 当前连接数：{command.Content}");
                        Task.Run(() =>
                        {
                            try
                            {
                                _ServiceProviderAllocator.SetClientConnectQuantity(this.ServiceInfo,Convert.ToInt32(command.Content));
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
