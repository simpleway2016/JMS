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
        IRegisterServiceManager _registerServiceManager;
        ILogger<ServiceProviderAllocator> _logger;
        ServiceRunningItem[] _serviceInfos;

        public ServiceProviderAllocator(ILogger<ServiceProviderAllocator> logger, IRegisterServiceManager registerServiceManager)
        {
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
                if (_serviceInfos == null)
                {
                    _serviceInfos = serviceInfos.Select(m => new ServiceRunningItem(_logger)
                    {
                        ServiceInfo = m
                    }).ToArray();
                }
                else
                {
                    List<ServiceRunningItem> ret = new List<ServiceRunningItem>();

                    foreach (var info in serviceInfos)
                    {
                        var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.ServiceId == info.ServiceId);
                        if (item != null)
                        {
                            item.ServiceInfo = info;
                            ret.Add(item);
                        }
                        else
                        {
                            ret.Add(new ServiceRunningItem(_logger)
                            {
                                ServiceInfo = info
                            });
                        }
                    }

                    _serviceInfos = ret.ToArray();
                }
            }
        }

       
        public RegisterServiceLocation Alloc(GetServiceProviderRequest request)
        {
            var matchServices = _serviceInfos.Where(m => m.ServiceInfo.ServiceNames.Contains(request.ServiceName)
            && m.ServiceInfo.MaxThread > 0
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
          
            return new RegisterServiceLocation
            {
                Host = item.ServiceInfo.Host,
                ServiceAddress = item.ServiceInfo.ServiceAddress,
                Port = item.ServiceInfo.Port
            };
        }

    }

   
}
