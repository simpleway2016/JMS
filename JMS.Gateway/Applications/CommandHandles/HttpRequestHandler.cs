using JMS.Domains;
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
using JMS.Infrastructures;
using System.Reflection.PortableExecutable;
using JMS.ServerCore.Http;

namespace JMS.Applications.CommandHandles
{
    /// <summary>
    /// 处理http请求
    /// </summary>
    class HttpRequestHandler : ICommandHandler
    {
        IServiceProviderAllocator _serviceProviderAllocator;
        IRegisterServiceManager _registerServiceManager;
        ICommandHandlerRoute _commandHandlerRoute;
        IHttpMiddlewareManager _httpMiddlewareManager;
        public HttpRequestHandler(IServiceProviderAllocator serviceProviderAllocator, IRegisterServiceManager registerServiceManager,
            ICommandHandlerRoute commandHandlerRoute, IHttpMiddlewareManager httpMiddlewareManager)
        {
            this._serviceProviderAllocator = serviceProviderAllocator;
            this._registerServiceManager = registerServiceManager;
            this._commandHandlerRoute = commandHandlerRoute;
            this._httpMiddlewareManager = httpMiddlewareManager;
        }
        public CommandType MatchCommandType => CommandType.HttpRequest;

        public async Task Handle(NetClient client, GatewayCommand cmd)
        {
            if (cmd.Header == null)
            {
                cmd.Header = new Dictionary<string, string>();
            }

            var requestPathLine = await JMS.ServerCore.HttpHelper.ReadHeaders( client.PipeReader, cmd.Header);
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
