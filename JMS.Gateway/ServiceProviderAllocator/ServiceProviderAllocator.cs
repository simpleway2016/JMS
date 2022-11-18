using JMS.Dtos;
using JMS.Domains;
using Microsoft.Extensions.Logging;
using Natasha.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS
{
    /// <summary>
    /// 微服务分配器，轮流分配原则
    /// </summary>
    class ServiceProviderAllocator : IServiceProviderAllocator
    {
        ClientCheckProxyFactory _clientCheckProxyFactory;
        IRegisterServiceManager _registerServiceManager;
        ILogger<ServiceProviderAllocator> _logger;
        ServiceRunningItem[] _serviceRunningItems;

        public ServiceProviderAllocator(ILogger<ServiceProviderAllocator> logger, ClientCheckProxyFactory clientCheckProxyFactory, IRegisterServiceManager registerServiceManager)
        {
            this._clientCheckProxyFactory = clientCheckProxyFactory;
            this._registerServiceManager = registerServiceManager;
            this._logger = logger;
            _registerServiceManager.ServiceConnect += _registerServiceManager_ServiceConnect;
            _registerServiceManager.ServiceDisconnect += _registerServiceManager_ServiceConnect;
        }

        private void _registerServiceManager_ServiceConnect(object sender, RegisterServiceInfo e)
        {
            lock (this)
            {
                RegisterServiceInfo[] serviceInfos = _registerServiceManager.GetAllRegisterServices().ToArray();
                if (_serviceRunningItems == null)
                {
                    _serviceRunningItems = serviceInfos.Select(m => new ServiceRunningItem(_logger, _clientCheckProxyFactory)
                    {
                        ServiceInfo = m
                    }).ToArray();
                }
                else
                {
                    List<ServiceRunningItem> items = new List<ServiceRunningItem>();

                    foreach (var info in serviceInfos)
                    {
                        var item = _serviceRunningItems.FirstOrDefault(m => m.ServiceInfo.ServiceId == info.ServiceId);
                        if (item != null)
                        {
                            item.ServiceInfo = info;
                            items.Add(item);
                        }
                        else
                        {
                            items.Add(new ServiceRunningItem(_logger, _clientCheckProxyFactory)
                            {
                                ServiceInfo = info
                            });
                        }
                    }

                    _serviceRunningItems = items.ToArray();
                }
            }
        }

       
        public RegisterServiceLocation Alloc(GetServiceProviderRequest request)
        {
            var matchServices = _serviceRunningItems.Where(m => m.ServiceInfo.ServiceNames.Contains(request.ServiceName)
            && m.ServiceInfo.MaxThread > 0
            && (m.ServiceInfo.MaxRequestCount == 0 || m.ServiceInfo.RequestQuantity < m.ServiceInfo.MaxRequestCount)
            && (m.ClientChecker == null || m.ClientChecker.Check(request.Header)));

            if (request.IsGatewayProxy)
            {
                //如果是来自网关的反向代理访问，只能匹配支持反向代理的服务
                matchServices = matchServices.Where(m => m.ServiceInfo.GatewayProxy == true);
            }

            IEnumerable<ServiceRunningItem> services = null ;

            //先查找cpu使用率低于70%的
            services = matchServices.Where(m => m.ServiceInfo.CpuUsage < 70);


            if (services.Count() == 0)
                services = matchServices;

            //查找一个客户占用比较低的机器
            var item = services.OrderBy(m => m.ServiceInfo.RequestQuantity / m.ServiceInfo.MaxThread).FirstOrDefault();
            if (item == null)
                return null;

            Interlocked.Increment(ref item.ServiceInfo.RequestQuantity);
          
            return new RegisterServiceLocation
            {
                Host = item.ServiceInfo.Host,
                ServiceAddress = item.ServiceInfo.ServiceAddress,
                Port = item.ServiceInfo.Port,
                GatewayProxy = item.ServiceInfo.GatewayProxy,
            };
        }

    }

   
}
