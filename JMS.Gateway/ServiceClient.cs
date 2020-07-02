using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    class ServiceClient
    {
        ILogger<ServiceClient> _Logger;
        Gateway _Gateway;
        internal CommandHandler _commandHandler;
        public RegisterServiceInfo ServiceInfo { get; set; }
        IServiceProviderAllocator _ServiceProviderAllocator;
        public ServiceClient(ILogger<ServiceClient> logger, Gateway gateway, IServiceProviderAllocator serviceProviderAllocator)
        {
            _Gateway = gateway;
            _ServiceProviderAllocator = serviceProviderAllocator;
               _Logger = logger;
        }
        public void Init(GatewayCommand registerCmd)
        {
            ServiceInfo = registerCmd.Content.FromJson<RegisterServiceInfo>();
            ServiceInfo.Host = _commandHandler.Host.Address.ToString();
            _commandHandler.Write(new byte[] { 0x1 });
            lock(_Gateway.ServiceClients)
            {
                _Gateway.ServiceClients.Add(this);                
            }
            Task.Run(() => {
                _ServiceProviderAllocator.ServiceInfoChanged(_Gateway.GetAllServiceProviders());
            });
            _Logger?.LogInformation($"微服务{this.ServiceInfo.ServiceNames.ToJsonString()} {this.ServiceInfo.Host}:{this.ServiceInfo.Port}注册");
            new Thread(checkState).Start();
        }

        void disconnect()
        {
            _commandHandler.Dispose();
            lock (_Gateway.ServiceClients)
            {
                _Gateway.ServiceClients.Remove(this);
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
                   var command = _commandHandler.ReadCommand();
                    if(command.Type == CommandType.ReportClientConnectQuantity)
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
