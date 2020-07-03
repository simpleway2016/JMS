using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public interface IMicroService
    {
        /// <summary>
        /// 获取微服务的客户端c#代码（只在debug模式下有效）
        /// </summary>
        /// <param name="nameSpace">代码使用什么命名空间</param>
        /// <param name="className">类名</param>
        /// <returns></returns>
        string GetServiceClassCode(string nameSpace,string className);
        void Invoke(string method, params object[] parameters);
        T Invoke<T>(string method, params object[] parameters);
        Task<T> InvokeAsync<T>(string method, params object[] parameters);
        Task InvokeAsync(string method, params object[] parameters);
    }
}
