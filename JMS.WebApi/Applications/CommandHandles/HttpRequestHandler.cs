
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Web;
using Microsoft.CodeAnalysis;
using System.Reflection.PortableExecutable;
using JMS.ServerCore.Http;
using System.IO.Pipelines;
using System.Buffers;
using JMS.ServerCore;
using System.Net;
using Microsoft.Extensions.Configuration;
namespace JMS.Applications.CommandHandles
{
    /// <summary>
    /// 处理http请求
    /// </summary>
    class HttpRequestHandler
    {
        IHttpMiddlewareManager _httpMiddlewareManager;
        private readonly RequestTimeLimter _requestTimeLimter;
        Common.ConfigurationValue<string[]> _proxyIps;
        public HttpRequestHandler(IHttpMiddlewareManager httpMiddlewareManager, RequestTimeLimter requestTimeLimter,IConfiguration configuration)
        {
            this._httpMiddlewareManager = httpMiddlewareManager;
            this._requestTimeLimter = requestTimeLimter;
            _proxyIps = configuration.GetSection("ProxyIps").GetNewest<string[]>();
        }

        public async Task Handle(NetClient client, bool redirectHttps)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var requestPathLine = await client.PipeReader.ReadHeaders( headers);

            if (_requestTimeLimter.LimitSetting.Current != null && _requestTimeLimter.LimitSetting.Current.Limit > 0)
            {
                headers.TryGetValue("X-Forwarded-For", out string x_for);

                var ip = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
                ip = RequestTimeLimter.GetRemoteIpAddress(_proxyIps.Current, ip, x_for);
                if (_requestTimeLimter.OnRequesting(ip) == false)
                {
                    //输出401
                    client.KeepAlive = false;
                    client.OutputHttpCode(401, "Forbidden...");
                    return;
                }
            }

            var requestPathLineArr = requestPathLine.Split(' ');
            var method = requestPathLineArr[0];
            var requestPath = requestPathLineArr[1];

            if (redirectHttps)
            {
                client.OutputHttpRedirect301($"https://{headers["Host"]}{requestPath}");
                return;
            }

            if (headers.TryGetValue("Connection", out string connection) && string.Equals(connection, "keep-alive", StringComparison.OrdinalIgnoreCase))
            {
                client.KeepAlive = true;
            }
            else if(connection == null)
            {
                client.KeepAlive = true;
            }

            await _httpMiddlewareManager.Handle(client, method, requestPath, headers);
        }

      
    }
}
