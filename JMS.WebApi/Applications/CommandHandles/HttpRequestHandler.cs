
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

        public async Task Handle(NetClient client, GatewayCommand cmd,bool redirectHttps)
        {

            if (cmd.Header == null)
            {
                cmd.Header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var requestPathLine = await client.PipeReader.ReadHeaders( cmd.Header);
            var requestPathLineArr = requestPathLine.Split(' ');
            var method = requestPathLineArr[0];
            var requestPath = requestPathLineArr[1];

            if (redirectHttps)
            {
                client.OutputHttpRedirect301($"https://{cmd.Header["Host"]}{requestPath}");
                return;
            }

            if (cmd.Header.TryGetValue("Connection", out string connection) && string.Equals(connection, "keep-alive", StringComparison.OrdinalIgnoreCase))
            {
                client.KeepAlive = true;
            }
            else if(connection == null)
            {
                client.KeepAlive = true;
            }

            await _httpMiddlewareManager.Handle(client, method, requestPath, cmd.Header);
        }

      
    }
}
