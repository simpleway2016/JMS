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
            if (_manager == null)
            {
                //不能在构造函数获取_manager
                _manager = _serviceProvider.GetService<ICommandHandlerRoute>();
            }
            client.KeepAlive = true;

            List<byte> lineBuffer = new List<byte>(1024);
            string preline = null;
            string line = null;
            string requestPathLine = null;
            int contentLength = 0;
            if(cmd.Header == null)
            {
                cmd.Header = new Dictionary<string, string>();
            }
            while (true)
            {
                int bData = client.InnerStream.ReadByte();
                if (bData == 13)
                {
                    preline = line;
                    line = Encoding.UTF8.GetString(lineBuffer.ToArray());
                    lineBuffer.Clear();
                    if (requestPathLine == null)
                        requestPathLine = line;

                    if (line == "")
                    {
                        bData = client.InnerStream.ReadByte();
                        if (cmd.Header.ContainsKey("Content-Length"))
                        {
                            int.TryParse(cmd.Header["Content-Length"], out contentLength);
                        }
                        if (contentLength > 0)
                        {
                            client.ReceiveDatas(new byte[contentLength], 0, contentLength);
                        }
                        break;
                    }
                    else if(line.Contains(":"))
                    {
                        var arr = line.Split(':');
                        if (arr.Length >= 2)
                        {
                            var key = arr[0].Trim();
                            var value = arr[1].Trim();
                            if (cmd.Header.ContainsKey(key) == false)
                            {
                                cmd.Header[key] = value;
                            }
                        }
                    }
                }
                else if (bData != 10)
                {
                    lineBuffer.Add((byte)bData);
                }
            }

            var httpRequest = requestPathLine.Split(' ')[1];
            if (httpRequest.StartsWith("/?GetServiceProvider=") || httpRequest.StartsWith("/?GetAllServiceProviders") || httpRequest.StartsWith("/?FindMaster"))
            {
                httpRequest = httpRequest.Substring(2);
                if (httpRequest.Contains("="))
                {
                    var arr = httpRequest.Split('=');
                    var json = HttpUtility.UrlDecode(arr[1], Encoding.UTF8);
                    if (!json.StartsWith("{"))
                    {
                        json = new GetServiceProviderRequest { ServiceName = json }.ToJsonString();
                    }
                    cmd = new GatewayCommand
                    {
                        Type = (CommandType)Enum.Parse(typeof(CommandType), arr[0]),
                        Content = json,
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
                try
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
                catch
                {
                    client.OutputHttpNotFund();
                }
            }
        }
    }
}
