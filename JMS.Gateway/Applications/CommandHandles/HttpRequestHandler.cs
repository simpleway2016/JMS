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

        public void Handle(NetClient client, GatewayCommand cmd)
        {
            if(_manager == null)
            {
                //不能在构造函数获取_manager
                _manager = _serviceProvider.GetService<ICommandHandlerRoute>();
            }
            client.KeepAlive = false;

            List<byte> buffer = null;
            var data = new byte[1024];
            string questLine = null;
            int readed;

            while (true)
            {
                readed = client.Socket.Receive(data, data.Length, SocketFlags.None);
                if (readed <= 0)
                    return;

                if (data.Any(m => m == 10))
                {
                    if (buffer != null)
                    {
                        buffer.AddRange(data);
                        data = buffer.ToArray();
                    }
                    questLine = Encoding.UTF8.GetString(data);
                    questLine = questLine.Substring(0, questLine.IndexOf("\r"));
                    break;
                }
                else
                {
                    if (buffer == null)
                    {
                        buffer = new List<byte>();
                    }
                    buffer.AddRange(data);
                }
            }

            var httpRequest = questLine.Split(' ')[1];
            if (httpRequest.StartsWith("/?GetServiceProvider=") || httpRequest.StartsWith("/?GetAllServiceProviders"))
            {
                httpRequest = httpRequest.Substring(2);
                if (httpRequest.Contains("="))
                {
                    var arr = httpRequest.Split('=');
                    cmd = new GatewayCommand
                    {
                        Type = (CommandType)Enum.Parse(typeof(CommandType), arr[0]),
                        Content = HttpUtility.UrlDecode(arr[1], Encoding.UTF8),
                        IsHttp = true
                    };
                    _manager.AllocHandler(cmd)?.Handle(client, cmd);
                }
                else
                {
                    cmd = new GatewayCommand
                    {
                        Type = (CommandType)Enum.Parse(typeof(CommandType), httpRequest),
                        IsHttp = true
                    };
                    _manager.AllocHandler(cmd)?.Handle(client, cmd);
                }
            }
            else
            {
                var servieName = httpRequest.Substring(1);
                servieName = servieName.Substring(0, servieName.IndexOf("/"));

                httpRequest = httpRequest.Substring(servieName.Length + 1);
                //重定向
                var location = _serviceProviderAllocator.Alloc(new GetServiceProviderRequest
                {
                    ServiceName = servieName
                });
                if (location != null && !string.IsNullOrEmpty(location.ServiceAddress))
                {
                    var serverAddr = location.ServiceAddress;
                    if (serverAddr.EndsWith("/"))
                        serverAddr = serverAddr.Substring(0, serverAddr.Length);

                    client.OutputHttpRedirect($"{serverAddr}{httpRequest}");
                }
                else
                {
                    client.OutputHttpNotFund();
                }
            }
        }
    }
}
