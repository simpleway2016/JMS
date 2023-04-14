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
        IServiceProvider _serviceProvider;
        ICommandHandlerRoute _manager;
        IServiceProviderAllocator _serviceProviderAllocator;
        IRegisterServiceManager _registerServiceManager;
        IHttpMiddlewareManager _httpMiddlewareManager;
        public HttpRequestHandler(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;

            _serviceProviderAllocator = serviceProvider.GetService<IServiceProviderAllocator>();
            _registerServiceManager = _serviceProvider.GetService<IRegisterServiceManager>();
            _manager = _serviceProvider.GetService<ICommandHandlerRoute>();
            _httpMiddlewareManager = _serviceProvider.GetService<IHttpMiddlewareManager>();
        }
        public CommandType MatchCommandType => CommandType.HttpRequest;

        public async Task Handle(NetClient client, GatewayCommand cmd)
        {
            if (cmd.Header == null)
            {
                cmd.Header = new Dictionary<string, string>();
            }

            var requestPathLine = await JMS.ServerCore.HttpHelper.ReadHeaders( cmd.Content, client.InnerStream, cmd.Header);
            var requestPathLineArr = requestPathLine.Split(' ');
            var method = requestPathLineArr[0];
            var requestPath = requestPathLineArr[1];

            //int contentLength = 0;
            //if (cmd.Header.ContainsKey("Content-Length"))
            //{
            //    int.TryParse(cmd.Header["Content-Length"], out contentLength);
            //}

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
