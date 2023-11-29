using System;
using System.Collections.Generic;
using System.Text;
using static MicroServiceControllerBase;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Collections.Specialized;
using System.Net;
using System.Linq;

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
                    _Headers = RequestingObject.Value.Command.Header;
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
        }

        /// <summary>
        /// 验证当前用户身份，并返回身份信息
        /// </summary>
        /// <returns></returns>
        public virtual object Authenticate(string token)
        {
            var auth = ServiceProvider?.GetService<IAuthenticationHandler>();
            if (auth != null)
            {
                return auth.VerifyToken(token);
            }
            return null;
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
                if(trustXForwardedFor.Contains(remoteIpAddr))
                {
                    for(int i = x_forArr.Length - 1; i >= 0; i--)
                    {
                        var ip = x_forArr[i];
                        if (trustXForwardedFor.Contains(ip) == false)
                            return ip;
                    }
                }
                else
                {
                    return remoteIpAddr;
                }
            }

            return remoteIpAddr;
        }
    }

    internal class LocalObject
    {
        public Dtos.InvokeCommand Command;
        public IServiceProvider ServiceProvider;
        public ClaimsPrincipal UserContent;
        public string RequestPath;
        public NameValueCollection RequestQuery;
        public EndPoint RemoteEndPoint;
        internal LocalObject(EndPoint remoteEndPoint, Dtos.InvokeCommand command, IServiceProvider serviceProvider, ClaimsPrincipal userContent)
        {
            this.RemoteEndPoint = remoteEndPoint;
            this.Command = command;
            this.ServiceProvider = serviceProvider;
            this.UserContent = userContent;
        }

        internal LocalObject(EndPoint remoteEndPoint, Dtos.InvokeCommand command, IServiceProvider serviceProvider, ClaimsPrincipal userContent, string requestPath)
        {
            this.RemoteEndPoint = remoteEndPoint;
            this.Command = command;
            this.ServiceProvider = serviceProvider;
            this.UserContent = userContent;
            this.RequestPath = requestPath;
        }
    }
}
