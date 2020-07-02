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
        public void SetClientConnectQuantity(RegisterServiceInfo from,int quantity)
        {
            var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.Host == from.Host && m.ServiceInfo.Port == from.Port);
            if(item != null)
            {
                Interlocked.Exchange(ref item.ClientQuantity , quantity);
                item.Usage = item.ClientQuantity / (decimal)item.ServiceInfo.MaxThread;
            }
        }
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
        public int GetClientConnectQuantity(RegisterServiceInfo from)
        {
            var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.Host == from.Host && m.ServiceInfo.Port == from.Port);
            if (item != null)
            {
                return item.ClientQuantity;
            }
            return 0;
        }
        public RegisterServiceLocation Alloc(GetServiceProviderRequest request)
        {
            var matchServices = _serviceInfos.Where(m => m.ServiceInfo.ServiceNames.Contains(request.ServiceName));

            //查找一个客户占用比较低的机器
            var item = matchServices.OrderBy(m => m.Usage).FirstOrDefault();
            Interlocked.Increment(ref item.ClientQuantity);
            item.Usage = item.ClientQuantity / (decimal)item.ServiceInfo.MaxThread;
            return new RegisterServiceLocation { 
                Host = item.ServiceInfo.Host,
                Port = item.ServiceInfo.Port
            };
        }

      
    }

    class ServiceProviderCounter
    {
        public RegisterServiceInfo ServiceInfo;
        /// <summary>
        /// 当前客户总数
        /// </summary>
        public int ClientQuantity;
        public decimal Usage;
    }
}
