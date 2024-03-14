using JMS.ServerCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.ServerCore
{
    public static class Extens
    {
        /// <summary>
        /// 使用http中间件
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IHttpMiddlewareManager UseHttp(this JmsServiceCollection services)
        {
            var manager = new HttpMiddlewareManager(services);
            services.AddSingleton<IHttpMiddlewareManager>(manager);
            return manager;
        }

        public static IHttpMiddlewareManager AddHttpMiddleware<T>(this IHttpMiddlewareManager httpMiddlewareManager) where T : IHttpMiddleware
        {
            httpMiddlewareManager.AddHttpMiddleware<T>();
            return httpMiddlewareManager;
        }
    }
}
