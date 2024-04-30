using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public interface IMicroService
    {
        /// <summary>
        /// 微服务地址是否是由网关分配
        /// </summary>
        bool IsFromGateway { get; }
        /// <summary>
        /// 服务器地址
        /// </summary>
        ClientServiceDetail ServiceLocation { get; }
        /// <summary>
        /// 获取微服务的客户端c#代码
        /// </summary>
        /// <param name="nameSpace">代码使用什么命名空间</param>
        /// <param name="className">类名</param>
        /// <returns></returns>
        string GetServiceClassCode(string nameSpace,string className);
        /// <summary>
        /// 获取微服务的客户端c#代码
        /// </summary>
        /// <param name="nameSpace">代码使用什么命名空间</param>
        /// <param name="className">类名</param>
        /// <returns></returns>
        Task<string> GetServiceClassCodeAsync(string nameSpace, string className);
        /// <summary>
        /// 获取微服务的方法描述
        /// </summary>
        /// <returns></returns>
        string GetServiceInfo();
        /// <summary>
        /// 获取微服务的方法描述
        /// </summary>
        /// <returns></returns>
        Task<string> GetServiceInfoAsync();
        void Invoke(string method, params object[] parameters);
        T Invoke<T>(string method, params object[] parameters);
        Task<T> InvokeAsync<T>(string method, params object[] parameters);
        Task InvokeAsync(string method, params object[] parameters);

        Task<InvokeResult<T>> InvokeExAsync<T>(string method, params object[] parameters);
    }
}
