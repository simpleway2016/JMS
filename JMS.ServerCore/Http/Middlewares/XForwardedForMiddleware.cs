using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace JMS.ServerCore.Http.Middlewares
{
    internal class XForwardedForMiddleware : IHttpMiddleware
    {
        private readonly ILogger<XForwardedForMiddleware> _logger;

        public XForwardedForMiddleware(ILogger<XForwardedForMiddleware> logger)
        {
            _logger = logger;
        }

        public Task<bool> Handle(NetClient netClient, string httpMethod, string requestPath, IDictionary<string, string> headers)
        {
            var ip = ((IPEndPoint)netClient.Socket.RemoteEndPoint).Address.ToString();
            if (headers.TryGetValue("X-Forwarded-For", out string xff))
            {
                _logger.LogInformation($"有X-Forwarded-For头，值：{xff}  当前ip:{ip}");
                if (xff.Contains(ip) == false)
                    xff += $", {ip}";
            }
            else
            {
                headers["X-Forwarded-For"] = ip;
            }
            return Task.FromResult(false);
        }
    }
}
