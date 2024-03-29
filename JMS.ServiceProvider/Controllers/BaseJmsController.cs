﻿using System;
using System.Collections.Generic;
using System.Text;
using static MicroServiceControllerBase;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Collections.Specialized;
using System.Net;
using System.Linq;
using System.IO;

namespace JMS
{
    public class BaseJmsController
    {
        internal static AsyncLocal<LocalObject> RequestingObject = new AsyncLocal<LocalObject>();

        private IDictionary<string, string> _Headers;
        /// <summary>
        /// 请求的头
        /// </summary>
        public IDictionary<string, string> Headers
        {
            get
            {
                if (_Headers == null && RequestingObject.Value != null)
                {
                    _Headers = RequestingObject.Value.Headers;
                }
                return _Headers;
            }
        }


        ClaimsPrincipal _userContent;
        /// <summary>
        /// 身份验证后获取的身份信息
        /// </summary>
        public ClaimsPrincipal UserContent
        {
            get
            {
                if (_userContent == null && RequestingObject.Value != null)
                {
                    _userContent = RequestingObject.Value.UserContent;
                }
                return _userContent;
            }
        }

        IServiceProvider _ServiceProvider;
        /// <summary>
        /// Controller的依赖注入服务提供者
        /// </summary>
        public IServiceProvider ServiceProvider
        {
            get
            {
                if (_ServiceProvider == null && RequestingObject.Value != null)
                {
                    _ServiceProvider = RequestingObject.Value.ServiceProvider;
                }
                return _ServiceProvider;
            }
            set
            {
                _ServiceProvider = value;
            }
        }

        /// <summary>
        /// 验证当前用户身份，并把身份信息赋值给UserContent
        /// </summary>
        /// <returns></returns>
        public virtual void Authenticate(string token)
        {
            var auth = ServiceProvider?.GetService<IAuthenticationHandler>();
            if (auth != null)
            {
                this._userContent = auth.VerifyToken(token);
            }
            else
                throw new Exception("未注册身份验证组件");
        }

        /// <summary>
        /// 获取客户端真实地址
        /// </summary>
        /// <param name="trustXForwardedFor">受信任的X-Forwarded-For节点地址
        /// 这个地址应该包括webapi、nginx等反向代理的ip地址
        /// </param>
        /// <returns></returns>
        public string GetRemoteIpAddress(string[] trustXForwardedFor)
        {
            var remoteIpAddr = ((IPEndPoint)RequestingObject.Value.RemoteEndPoint).Address.ToString();
            if(trustXForwardedFor != null && trustXForwardedFor.Length > 0 && this.Headers.TryGetValue("X-Forwarded-For" , out string x_for))
            {
                var x_forArr = x_for.Split(',').Select(m => m.Trim()).Where(m => m.Length > 0).ToArray();
                for(int i = 0; i < x_forArr.Length; i++)
                {
                    var ip = x_forArr[i];
                    if (trustXForwardedFor.Contains(ip) && i > 0)
                        return x_forArr[i - 1];
                }

            }

            return remoteIpAddr;
        }
    }

    internal class LocalObject
    {
        public IDictionary<string,string> Headers;
        public IServiceProvider ServiceProvider;
        public ClaimsPrincipal UserContent;
        public string RequestPath;
        public NameValueCollection RequestQuery;
        public EndPoint RemoteEndPoint;
        internal LocalObject(EndPoint remoteEndPoint, Dtos.InvokeCommand command, IServiceProvider serviceProvider, ClaimsPrincipal userContent)
        {
            this.RemoteEndPoint = remoteEndPoint;
            this.Headers = command.Header;
            this.ServiceProvider = serviceProvider;
            this.UserContent = userContent;
        }

        internal LocalObject(EndPoint remoteEndPoint, Dtos.InvokeCommand command, IServiceProvider serviceProvider, ClaimsPrincipal userContent, string requestPath)
        {
            this.RemoteEndPoint = remoteEndPoint;
            this.Headers = command.Header;
            this.ServiceProvider = serviceProvider;
            this.UserContent = userContent;
            this.RequestPath = requestPath;
        }
    }
}
