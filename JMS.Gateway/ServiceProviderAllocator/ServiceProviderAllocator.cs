using JMS.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using JMS.Applications;

namespace JMS
{
    /// <summary>
    /// 微服务分配器，轮流分配原则
    /// </summary>
    class ServiceProviderAllocator : IServiceProviderAllocator
    {
        ClientCheckFactory _clientCheckProxyFactory;
        IRegisterServiceManager _registerServiceManager;
        ILogger<ServiceProviderAllocator> _logger;
        ServiceRunningItem[] _serviceRunningItems;

        public ServiceProviderAllocator(ILogger<ServiceProviderAllocator> logger, ClientCheckFactory clientCheckProxyFactory, IRegisterServiceManager registerServiceManager)
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

        static string[] loopbackIps = new string[] { "127.0.0.1", "::1" };
        bool isSameIp(string ip1,string ip2)
        {
            if (ip1 == ip2)
                return true;
            if (loopbackIps.Contains(ip1) && loopbackIps.Contains(ip2))
                return true;
            return false;
        }
       
        public ClientServiceDetail Alloc(GetServiceProviderRequest request)
        {
            if (_serviceRunningItems == null)
                return null;
            var matchServices = _serviceRunningItems.Where(m => m.ServiceInfo.ServiceList.Any(n=>n.Name == request.ServiceName && (n.AllowGatewayProxy || request.IsGatewayProxy == false))
            && m.ServiceInfo.MaxThread > 0
            && (m.ServiceInfo.AllowHostIp == null || isSameIp(m.ServiceInfo.AllowHostIp , request.ClientAddress))
            && (m.ServiceInfo.MaxRequestCount == 0 || m.ServiceInfo.RequestQuantity < m.ServiceInfo.MaxRequestCount)
            && (m.ClientChecker == null || m.ClientChecker.Check(request.Header)));

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

            var detail = item.ServiceInfo.ServiceList.FirstOrDefault(n => n.Name == request.ServiceName && (n.AllowGatewayProxy || request.IsGatewayProxy == false));
            return new ClientServiceDetail(detail, item.ServiceInfo);
        }

    }

   
}
