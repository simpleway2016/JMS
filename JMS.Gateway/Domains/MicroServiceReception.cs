using JMS.Dtos;
using JMS.Domains;
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

namespace JMS.Domains
{
    class MicroServiceReception : IMicroServiceReception
    {
        IRegisterServiceManager _registerServiceManager;
        ILogger<MicroServiceReception> _Logger;
        Gateway _Gateway;
        NetClient NetClient;
        public RegisterServiceInfo ServiceInfo { get; set; }

        IServiceProviderAllocator _ServiceProviderAllocator;
        LockKeyManager _lockKeyManager;

        bool _closed;
        public MicroServiceReception(ILogger<MicroServiceReception> logger,
            Gateway gateway,
            LockKeyManager lockKeyManager,
            IRegisterServiceManager registerServiceManager,
            IServiceProviderAllocator serviceProviderAllocator)
        {
            this._registerServiceManager = registerServiceManager;
            _Gateway = gateway;
            _ServiceProviderAllocator = serviceProviderAllocator;
            _Logger = logger;
            _lockKeyManager = lockKeyManager;
        }
        public void HealthyCheck( NetClient netclient, GatewayCommand registerCmd)
        {
            this.NetClient = netclient;
            ServiceInfo = registerCmd.Content.FromJson<RegisterServiceInfo>();

            if(ServiceInfo.SingletonService)
            {
                if (_registerServiceManager.GetAllRegisterServices().Any(m => string.Join(',', m.ServiceNames) == string.Join(',', ServiceInfo.ServiceNames)))
                {
                    NetClient.WriteServiceData(new InvokeResult
                    {
                        Success = false,
                        Error = "SingletonService"
                    });
                    return;
                }
            }

            ServiceInfo.Host = ((IPEndPoint)NetClient.Socket.RemoteEndPoint).Address.ToString();
            if (string.IsNullOrEmpty(ServiceInfo.ServiceAddress))
                ServiceInfo.ServiceAddress = ServiceInfo.Host;


            NetClient.WriteServiceData(new InvokeResult{ 
                Success = true
            });
            _registerServiceManager.AddRegisterService(this);

            _Logger?.LogInformation($"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}注册");

            checkState();
        }

        void disconnect()
        {
            _registerServiceManager.RemoveRegisterService(ServiceInfo);

        }
        public void Close()
        {
            _closed = true;
            disconnect();

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
                                Interlocked.Exchange(ref ServiceInfo.RequestQuantity, hardwareInfo.RequestQuantity.GetValueOrDefault());
                                ServiceInfo.CpuUsage = hardwareInfo.CpuUsage.GetValueOrDefault();
                            }
                            catch
                            {
                            }
                        });                       
                    }
                }
                catch(System.ObjectDisposedException)
                {
                    _Logger?.LogInformation($"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}断开");
                    if (_closed == false)
                    {
                        disconnect();
                    }
                    return;
                }
                catch(SocketException)
                {
                    _Logger?.LogInformation( $"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}断开");
                    if (_closed == false)
                    {
                        disconnect();
                    }
                    return;
                }
                catch (Exception ex)
                {
                    _Logger?.LogInformation(ex, $"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}断开");

                    if (_closed == false)
                    {
                        disconnect();
                    }
                    return;
                }
            }
        }
    }
}
