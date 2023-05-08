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
        public Task HealthyCheck(NetClient netclient, GatewayCommand registerCmd)
        {
            this.NetClient = netclient;
            handleRegister(registerCmd, this);
            if (this.ServiceInfo == null)
                return Task.CompletedTask;

            return checkState();
        }

        void handleRegister(GatewayCommand registerCmd, MicroServiceReception reception)
        {
            var serviceItem = registerCmd.Content.FromJson<RegisterServiceInfo>();
            if (serviceItem.ServiceList == null)
            {
                serviceItem.ServiceList = new ServiceDetail[serviceItem.ServiceNames.Length];
                for (int i = 0; i < serviceItem.ServiceList.Length; i++)
                {
                    var detail = new ServiceDetail
                    {
                        Name = serviceItem.ServiceNames[i],
                        Type = ServiceType.JmsService
                    };
                    if(serviceItem.Port == 0)
                    {
                        detail.Type = ServiceType.WebApi;
                        detail.AllowGatewayProxy = true;
                    }
                    serviceItem.ServiceList[i] = detail;
                }
            }


            if (serviceItem.SingletonService)
            {
                if (_registerServiceManager.GetAllRegisterServices().Any(m => string.Join(',', m.ServiceList.Select(n=>n.Name).ToArray()) == string.Join(',', serviceItem.ServiceList.Select(n => n.Name).ToArray())))
                {
                    NetClient.WriteServiceData(new InvokeResult
                    {
                        Success = false,
                        Error = "SingletonService"
                    });
                    return;
                }
            }

            serviceItem.Host = ((IPEndPoint)NetClient.Socket.RemoteEndPoint).Address.ToString();
            if (string.IsNullOrEmpty(serviceItem.ServiceAddress))
                serviceItem.ServiceAddress = serviceItem.Host;


            NetClient.WriteServiceData(new InvokeResult<string>
            {
                Success = true,
                Data = this.GetType().Assembly.GetName().Version.ToString()
            });

            reception.ServiceInfo = serviceItem;
            _registerServiceManager.AddRegisterService(reception);

            _Logger?.LogInformation($"微服务{serviceItem.ServiceList.Select(m=>m.Name).ToJsonString()} {serviceItem.Host}:{serviceItem.Port}注册");


        }

        void disconnect()
        {
            _registerServiceManager.RemoveRegisterService(ServiceInfo);

        }
        public virtual void Close()
        {
            _closed = true;
            disconnect();

            NetClient?.Dispose();
        }
        async Task checkState()
        {
            NetClient.ReadTimeout = 30000;
            while (!_closed)
            {
                try
                {
                    var command = await NetClient.ReadServiceObjectAsync<GatewayCommand>();

                    if (command.Type == CommandType.ReportClientConnectQuantity)
                    {
                        //微服务向我报告当前它的请求连接数
                        //_Logger?.LogDebug($"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port} 当前连接数：{command.Content}");
                        try
                        {
                            var hardwareInfo = command.Content.FromJson<PerformanceInfo>();
                            Interlocked.Exchange(ref ServiceInfo.RequestQuantity, hardwareInfo.RequestQuantity.GetValueOrDefault());
                            ServiceInfo.CpuUsage = hardwareInfo.CpuUsage.GetValueOrDefault();
                            _registerServiceManager.RefreshServiceInfo(ServiceInfo);
                        }
                        catch
                        {
                        }

                        NetClient.WriteServiceData(new InvokeResult
                        {
                            Success = true
                        });
                    }
                    else if (command.Type == CommandType.RegisterSerivce)
                    {
                        handleRegister(command, new VirtualServiceReception());
                    }
                    else
                    {
                        NetClient.WriteServiceData(new InvokeResult
                        {
                            Success = true
                        });
                    }
                }
                catch (System.ObjectDisposedException)
                {
                    _Logger?.LogInformation($"微服务{this.ServiceInfo.ServiceList.Select(m => m.Name).ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}断开");
                    if (_closed == false)
                    {
                        disconnect();
                    }
                    return;
                }
                catch (SocketException)
                {
                    _Logger?.LogInformation($"微服务{this.ServiceInfo.ServiceList.Select(m => m.Name).ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}断开");
                    if (_closed == false)
                    {
                        disconnect();
                    }
                    return;
                }
                catch (Exception ex)
                {
                    _Logger?.LogInformation(ex, $"微服务{this.ServiceInfo.ServiceList.Select(m => m.Name).ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}断开");

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
