using JMS.Common.Collections;
using JMS.ServerCore.Http.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS.ServerCore.Http
{
    public interface IHttpMiddlewareManager
    {
        IHttpMiddlewareManager AddHttpMiddleware<T>() where T : IHttpMiddleware;
        Task Handle(NetClient netClient, string httpMethod, string requestPath, IgnoreCaseDictionary headers);
        void PrepareMiddlewares(IServiceProvider serviceProvider);
    }

    public class HttpMiddlewareManager : IHttpMiddlewareManager
    {
        internal JmsServiceCollection Services;
        public HttpMiddlewareManager(JmsServiceCollection services)
        {
            this.Services = services;

            this.AddHttpMiddleware<OptionsMiddleware>()
              .AddHttpMiddleware<ErrorPathCheckMiddleware>()
              .AddHttpMiddleware<XForwardedForMiddleware>();
        }

        /// <summary>
        /// 准备所有中间件
        /// </summary>
        /// <param name="serviceProvider"></param>
        public void PrepareMiddlewares(IServiceProvider serviceProvider)
        {
            _httpMiddlewares = new List<IHttpMiddleware>();
            foreach ( var type in _middlewareTypes)
            {
                _httpMiddlewares.Add( (IHttpMiddleware)serviceProvider.GetService(type) );
            }
            _middlewareTypes.Clear();
        }

        List<IHttpMiddleware> _httpMiddlewares = null;
        List<Type> _middlewareTypes = new List<Type>();
        public async Task Handle(NetClient netClient, string httpMethod, string requestPath, IgnoreCaseDictionary headers)
        {
            if(_httpMiddlewares == null)
            {
                throw new Exception("IHttpMiddlewareManager.PrepareMiddlewares未被调用");
            }
            foreach( var middleware in _httpMiddlewares)
            {
                var ret = await middleware.Handle(netClient, httpMethod, requestPath, headers);
                if (ret)
                    break;
            }
        }


        public IHttpMiddlewareManager AddHttpMiddleware<T>() where T : IHttpMiddleware
        {
            var type = typeof(T);
            _middlewareTypes.Add(type);
            this.Services.AddSingleton(type);
            return this;
        }
    }
}
