﻿using JMS.Dtos;
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
            handleRegister(registerCmd , this);
            if (this.ServiceInfo == null)
                return;

            checkState();
        }

        void handleRegister(GatewayCommand registerCmd,MicroServiceReception reception)
        {
            var serviceItem = registerCmd.Content.FromJson<RegisterServiceInfo>();

            if (serviceItem.SingletonService)
            {
                if (_registerServiceManager.GetAllRegisterServices().Any(m => string.Join(',', m.ServiceNames) == string.Join(',', serviceItem.ServiceNames)))
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


            NetClient.WriteServiceData(new InvokeResult
            {
                Success = true
            });

            reception.ServiceInfo = serviceItem;
            _registerServiceManager.AddRegisterService(reception);

            _Logger?.LogInformation($"微服务{serviceItem.ServiceNames.ToJsonString()} {serviceItem.Host}:{serviceItem.Port}注册");
           

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
        void checkState()
        {
            NetClient.ReadTimeout = 30000;
            while(!_closed)
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
                                var hardwareInfo = command.Content.FromJson<PerformanceInfo>();
                                if (ServiceInfo.Port != 0)
                                {
                                    //只有端口不为0，才是JMS微服务，如果是0，可能是webapi，无法统计当前连接数
                                    Interlocked.Exchange(ref ServiceInfo.RequestQuantity, hardwareInfo.RequestQuantity.GetValueOrDefault());
                                }
                                ServiceInfo.CpuUsage = hardwareInfo.CpuUsage.GetValueOrDefault();
                            }
                            catch
                            {
                            }
                        });

                        NetClient.WriteServiceData(new InvokeResult
                        {
                            Success = true
                        });
                    }
                    else if(command.Type == CommandType.RegisterSerivce)
                    {
                        handleRegister(command , new VirtualServiceReception());
                    }
                    else
                    {
                        NetClient.WriteServiceData(new InvokeResult
                        {
                            Success = true
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
