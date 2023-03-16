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
        public HttpRequestHandler(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;

            _serviceProviderAllocator = serviceProvider.GetService<IServiceProviderAllocator>();
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

            var requestPath = requestPathLine.Split(' ')[1];
            if (requestPath.StartsWith("/?GetServiceProvider=") || requestPath.StartsWith("/?GetAllServiceProviders") || requestPath.StartsWith("/?FindMaster"))
            {
                if (contentLength > 0)
                {
                    await client.ReadDataAsync(new byte[contentLength], 0, contentLength);
                }

                requestPath = requestPath.Substring(2);
                if (requestPath.Contains("="))
                {
                    var arr = requestPath.Split('=');
                    var json = HttpUtility.UrlDecode(arr[1], Encoding.UTF8);
                    if (!json.StartsWith("{"))
                    {
                        json = new GetServiceProviderRequest { ServiceName = json }.ToJsonString();
                    }
                    cmd = new GatewayCommand
                    {
                        Type = (CommandType)Enum.Parse(typeof(CommandType), arr[0]),
                        Content = json,
                        Header = cmd.Header,
                        IsHttp = true
                    };
                    await _manager.AllocHandler(cmd)?.Handle(client, cmd);
                }
                else
                {
                    cmd = new GatewayCommand
                    {
                        Type = (CommandType)Enum.Parse(typeof(CommandType), requestPath),
                        Header = cmd.Header,
                        IsHttp = true
                    };
                    await _manager.AllocHandler(cmd)?.Handle(client, cmd);
                }
            }
            else if (string.Equals(connection, "Upgrade", StringComparison.OrdinalIgnoreCase)
                && cmd.Header.TryGetValue("Upgrade",out string upgrade) 
                && string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
            {
                await HttpProxy.WebSocketProxy(client, _serviceProviderAllocator, requestPathLine, requestPath, cmd);
            }
            else
            {
                //以代理形式去做中转
                await HttpProxy.Proxy(client, _serviceProviderAllocator, requestPathLine, requestPath, contentLength, cmd);
                //HttpProxy.Redirect(client ,_serviceProviderAllocator, requestPath);
            }
        }

      
    }
}
