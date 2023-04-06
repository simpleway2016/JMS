
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

namespace JMS.Applications.CommandHandles
{
    /// <summary>
    /// 处理http请求
    /// </summary>
    class HttpRequestHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        ICommandHandlerRoute _manager;
        public HttpRequestHandler(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;

        }
        public CommandType MatchCommandType => CommandType.HttpRequest;

        public async Task Handle(NetClient client, GatewayCommand cmd)
        {
            if (_manager == null)
            {
                //不能在构造函数获取_manager
                _manager = _serviceProvider.GetService<ICommandHandlerRoute>();
            }

            if (cmd.Header == null)
            {
                cmd.Header = new Dictionary<string, string>();
            }

            var requestPathLine = await HttpProxy.ReadHeaders( cmd.Content, client, cmd.Header);
            var method = requestPathLine.Split(' ')[0];
           
            int contentLength = 0;
            if (cmd.Header.ContainsKey("Content-Length"))
            {
                int.TryParse(cmd.Header["Content-Length"], out contentLength);
            }

            if (cmd.Header.TryGetValue("Connection", out string connection) && string.Equals(connection, "keep-alive", StringComparison.OrdinalIgnoreCase))
            {
                client.KeepAlive = true;
            }
            else if(cmd.Header.ContainsKey("Connection") == false)
            {
                client.KeepAlive = true;
            }
            if (method == "OPTIONS")
            {
                client.OutputHttp204(cmd.Header);
                return;
            }
            var requestPath = requestPathLine.Split(' ')[1];
            if (string.Equals(connection, "Upgrade", StringComparison.OrdinalIgnoreCase)
                && cmd.Header.TryGetValue("Upgrade",out string upgrade) 
                && string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
            {
                await HttpProxy.WebSocketProxy(client,  requestPathLine, requestPath, cmd);
            }
            else
            {
                //以代理形式去做中转
                await HttpProxy.Proxy(client, requestPathLine, requestPath, contentLength, cmd);
                //HttpProxy.Redirect(client ,_serviceProviderAllocator, requestPath);
            }
        }

      
    }
}
