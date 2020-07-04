using JMS.Dtos;
using Org.BouncyCastle.Operators;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public interface IServiceProviderAllocator
    {
        /// <summary>
        /// 当服务列表发生变化时，此函数会被自动调用
        /// </summary>
        /// <param name="serviceInfos"></param>
        void ServiceInfoChanged(RegisterServiceInfo[] serviceInfos);
        /// <summary>
        /// 分配一个微服务
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        RegisterServiceLocation Alloc(GetServiceProviderRequest request);

        /// <summary>
        /// 获取当前微服务的客户端连接数
        /// </summary>
        /// <param name="serviceInfo"></param>
        /// <returns></returns>
        int GetClientConnectQuantity(RegisterServiceInfo serviceInfo);

        /// <summary>
        /// 设置当前微服务连接的请求数
        /// </summary>
        /// <param name="serviceInfo"></param>
        void SetClientConnectQuantity(RegisterServiceInfo serviceInfo,int quantity);
    }
}
