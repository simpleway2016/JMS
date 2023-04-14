
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

namespace JMS.Applications.CommandHandles
{
    /// <summary>
    /// 处理http请求
    /// </summary>
    class HttpRequestHandler
    {
        IHttpMiddlewareManager _httpMiddlewareManager;
        public HttpRequestHandler(IHttpMiddlewareManager httpMiddlewareManager)
        {
            this._httpMiddlewareManager = httpMiddlewareManager;

        }
        public async Task Handle(NetClient client, GatewayCommand cmd)
        {

            if (cmd.Header == null)
            {
                cmd.Header = new Dictionary<string, string>();
            }

            var requestPathLine = await JMS.ServerCore.HttpHelper.ReadHeaders(null, client.InnerStream, cmd.Header);
            var requestPathLineArr = requestPathLine.Split(' ');
            var method = requestPathLineArr[0];
            var requestPath = requestPathLineArr[1];

            if (cmd.Header.TryGetValue("Connection", out string connection) && string.Equals(connection, "keep-alive", StringComparison.OrdinalIgnoreCase))
            {
                client.KeepAlive = true;
            }
            else if(cmd.Header.ContainsKey("Connection") == false)
            {
                client.KeepAlive = true;
            }

            await _httpMiddlewareManager.Handle(client, method, requestPath, cmd.Header);
        }

      
    }
}
