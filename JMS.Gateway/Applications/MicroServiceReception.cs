using JMS.Dtos;
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

namespace JMS.Applications
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
            _registerServiceManager = registerServiceManager;
            _Gateway = gateway;
            _ServiceProviderAllocator = serviceProviderAllocator;
            _Logger = logger;
            _lockKeyManager = lockKeyManager;
        }
        public async Task HealthyCheck(NetClient netclient, GatewayCommand registerCmd)
        {
            NetClient = netclient;
            try
            {
                handleRegister(registerCmd);
                if (ServiceInfo == null)
                {
                    _registerServiceManager.RemoveRegisterService(this);
                    return;
                }
                else if(ServiceInfo.ServiceAddress == "127.0.0.1" || (ServiceInfo.Port == 0 && ServiceInfo.ServiceAddress.Contains("/127.0.0.1")))
                {
                    //注册为本地地址的微服务，只允许这个机器连接
                    ServiceInfo.AllowHostIp = ((IPEndPoint)netclient.Socket.RemoteEndPoint).Address.ToString();
                    _Logger?.LogInformation($"微服务{ServiceInfo.ServiceList.Select(m => m.Name).ToJsonString()} {ServiceInfo.Host}:{ServiceInfo.Port} 只允许{ServiceInfo.AllowHostIp}连接");
                }

                await checkState();
            }
            catch (ObjectDisposedException)
            {
                _Logger?.LogInformation($"微服务{ServiceInfo.ServiceList.Select(m => m.Name).ToJsonString()} {ServiceInfo.Host}:{ServiceInfo.Port}断开");
                throw;
            }
            catch (Exception)
            {
                _Logger?.LogInformation($"微服务{ServiceInfo.ServiceList.Select(m => m.Name).ToJsonString()} {ServiceInfo.Host}:{ServiceInfo.Port}断开");
                throw;
            }
            finally
            {
                _registerServiceManager.RemoveRegisterService(this);
            }
        }

        void handleRegister(GatewayCommand registerCmd)
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
                    if (serviceItem.Port == 0)
                    {
                        detail.Type = ServiceType.WebApi;
                        detail.AllowGatewayProxy = true;
                    }
                    serviceItem.ServiceList[i] = detail;
                }
            }


            if (serviceItem.SingletonService)
            {
                if (_registerServiceManager.GetAllRegisterServices().Any(m => string.Join(',', m.ServiceList.Select(n => n.Name).ToArray()) == string.Join(',', serviceItem.ServiceList.Select(n => n.Name).ToArray())))
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
                Data = GetType().Assembly.GetName().Version.ToString()
            });

            ServiceInfo = serviceItem;
            _registerServiceManager.AddRegisterService(this);

            _Logger?.LogInformation($"微服务{serviceItem.ServiceList.Select(m => m.Name).ToJsonString()} {serviceItem.Host}:{serviceItem.Port}注册");


        }


        public virtual void Close()
        {

            NetClient?.Dispose();
        }
        async Task checkState()
        {
            NetClient.ReadTimeout = 30000;
            while (!_closed)
            {
                var command = await NetClient.ReadServiceObjectAsync<GatewayCommand>();

                if (command.Type == (int)CommandType.ReportClientConnectQuantity)
                {
                    //微服务向我报告当前它的请求连接数
                    //_Logger?.LogDebug($"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port} 当前连接数：{command.Content}");
                    try
                    {
                        var hardwareInfo = command.Content.FromJson<PerformanceInfo>();
                        ServiceInfo.CpuUsage = hardwareInfo.CpuUsage.GetValueOrDefault();

                        if (ServiceInfo.RequestQuantity != hardwareInfo.RequestQuantity)
                        {
                            Interlocked.Exchange(ref ServiceInfo.RequestQuantity, hardwareInfo.RequestQuantity);
                            _registerServiceManager.RefreshServiceInfo(ServiceInfo);
                        }
                    }
                    catch
                    {
                    }

                    NetClient.WriteServiceData(new InvokeResult
                    {
                        Success = true
                    });
                }
                else
                {
                    NetClient.WriteServiceData(new InvokeResult
                    {
                        Success = true
                    });
                }
            }
        }
    }
}
