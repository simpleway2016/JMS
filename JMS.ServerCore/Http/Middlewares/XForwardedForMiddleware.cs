using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace JMS.ServerCore.Http.Middlewares
{
    internal class XForwardedForMiddleware : IHttpMiddleware
    {
        public Task<bool> Handle(NetClient netClient, string httpMethod, string requestPath, Dictionary<string, string> headers)
        {
            var ip = ((IPEndPoint)netClient.Socket.RemoteEndPoint).Address.ToString();
            if (headers.TryGetValue("X-Forwarded-For", out string xff))
            {
                if (xff.Contains(ip) == false)
                {
                    xff += $", {ip}";
                    headers["X-Forwarded-For"] = xff;
                }
            }
            else
            {
                headers["X-Forwarded-For"] = ip;
            }
            return Task.FromResult(false);
        }
    }
}
