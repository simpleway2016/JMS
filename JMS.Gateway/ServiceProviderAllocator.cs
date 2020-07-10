using JMS.Dtos;
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
    class ServiceProviderAllocator: IServiceProviderAllocator
    {
        ServiceProviderCounter[] _serviceInfos;

        public void ServiceInfoChanged(RegisterServiceInfo[] serviceInfos)
        {
            if (_serviceInfos == null)
            {
                _serviceInfos = serviceInfos.Select(m => new ServiceProviderCounter()
                {
                    ServiceInfo = m
                }).ToArray();
            }
            else
            {
                List<ServiceProviderCounter> ret = new List<ServiceProviderCounter>();

                foreach( var info in serviceInfos )
                {
                    var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.Host == info.Host && m.ServiceInfo.Port == info.Port);
                    if(item != null)
                    {
                        item.ServiceInfo.ServiceNames = info.ServiceNames;
                        ret.Add(item);
                    }
                    else
                    {
                        ret.Add(new ServiceProviderCounter()
                        {
                            ServiceInfo = info
                        });
                    }
                }

                _serviceInfos = ret.ToArray();
            }
        }
       
        public RegisterServiceLocation Alloc(GetServiceProviderRequest request)
        {
            var matchServices = _serviceInfos.Where(m => m.ServiceInfo.ServiceNames.Contains(request.ServiceName));

            //先查找cpu使用率低于70%的
            if(matchServices.Where(m => m.CpuUsage < 70).Count() > 0)
                matchServices = matchServices.Where(m => m.CpuUsage < 70);

            //查找一个客户占用比较低的机器
            var item = matchServices.OrderBy(m => m.Usage).FirstOrDefault();
            Interlocked.Increment(ref item.RequestQuantity);
            item.Usage = item.RequestQuantity / (decimal)item.ServiceInfo.MaxThread;
            return new RegisterServiceLocation { 
                Host = item.ServiceInfo.Host,
                Port = item.ServiceInfo.Port
            };
        }

        public PerformanceInfo GetPerformanceInfo(RegisterServiceInfo from)
        {
            var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.Host == from.Host && m.ServiceInfo.Port == from.Port);
            if (item != null)
            {
                return new PerformanceInfo { 
                    RequestQuantity = item.RequestQuantity,
                    CpuUsage = item.CpuUsage
                };
            }
            return null;
        }

        public void SetServicePerformanceInfo(RegisterServiceInfo from, PerformanceInfo performanceInfo)
        {
            var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.Host == from.Host && m.ServiceInfo.Port == from.Port);
            if (item != null)
            {
                Interlocked.Exchange(ref item.RequestQuantity, performanceInfo.RequestQuantity.GetValueOrDefault());
                item.CpuUsage = performanceInfo.CpuUsage.GetValueOrDefault();
                item.Usage = item.RequestQuantity / (decimal)item.ServiceInfo.MaxThread;
            }
        }
    }

    class ServiceProviderCounter
    {
        public RegisterServiceInfo ServiceInfo;
        /// <summary>
        /// 当前请求数量
        /// </summary>
        public int RequestQuantity;
        /// <summary>
        /// cpu使用率
        /// </summary>
        public double CpuUsage;
        public decimal Usage;
    }
}
