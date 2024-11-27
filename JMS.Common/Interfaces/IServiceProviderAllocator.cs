using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public interface IServiceProviderAllocator
    {
        /// <summary>
        /// 分配一个微服务
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        ClientServiceDetail Alloc(GetServiceProviderRequest request);

    }
}
