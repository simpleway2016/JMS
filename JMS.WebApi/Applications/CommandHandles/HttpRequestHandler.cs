
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
using Microsoft.Extensions.Logging;
namespace JMS.Applications.CommandHandles
{
    /// <summary>
    /// 处理http请求
    /// </summary>
    class HttpRequestHandler
    {
        IHttpMiddlewareManager _httpMiddlewareManager;
        private readonly ILogger<HttpRequestHandler> _logger;

        public HttpRequestHandler(IHttpMiddlewareManager httpMiddlewareManager,ILogger<HttpRequestHandler> logger)
        {
            this._httpMiddlewareManager = httpMiddlewareManager;
            _logger = logger;
        }

        public async Task Handle(NetClient client, GatewayCommand cmd)
        {

            if (cmd.Header == null)
            {
                cmd.Header = new Dictionary<string, string>();
            }

            var requestPathLine = await client.PipeReader.ReadHeaders( cmd.Header);
            _logger.LogDebug(cmd.Header.ToJsonString(true));
            var requestPathLineArr = requestPathLine.Split(' ');
            var method = requestPathLineArr[0];
            var requestPath = requestPathLineArr[1];

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
