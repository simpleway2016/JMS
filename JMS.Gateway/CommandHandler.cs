using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Way.Lib;

namespace JMS
{
    class CommandHandler : IDisposable
    {
        IServiceProvider _serviceProvider;
        internal Way.Lib.NetStream Client;
        public IPEndPoint Host { private set; get; }
        ILogger<CommandHandler> _logger;
        Gateway _Gateway;
        TransactionIdBuilder _TransactionIdBuilder;
        public CommandHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetService<ILogger<CommandHandler>>();
            _Gateway = serviceProvider.GetService<Gateway>();
            _TransactionIdBuilder = serviceProvider.GetService<TransactionIdBuilder>();
        }

        public GatewayCommand ReadCommand()
        {
            return Client.ReadServiceObject<GatewayCommand>();
        }

        public void Write(byte[] data)
        {
            Client.Write(data.Length);
            Client.Write(data);
        }
        public void Write(object value)
        {
            this.Write(Encoding.UTF8.GetBytes(value.ToJsonString()));
        }

        public RegisterServiceLocation[] List(string serviceName)
        {
            if (serviceName == "")
            {
                var allocator = _serviceProvider.GetService<IServiceProviderAllocator>();
                return _Gateway.GetAllServiceProviders().Select(m => new RegisterServiceRunningInfo
                {
                    Host = m.Host,
                    Port = m.Port,
                    ServiceNames = m.ServiceNames,
                    MaxThread = m.MaxThread,
                    ClientConnected = allocator.GetClientConnectQuantity(m)
                }).ToArray();
            }
            else
            {
                return _Gateway.GetAllServiceProviders().Select(m => new RegisterServiceLocation
                {
                    Host = m.Host,
                    Port = m.Port
                }).ToArray();
            }
        }

        public void Handle(Socket socket)
        {
            this.Host = (IPEndPoint)socket.RemoteEndPoint;
            Client = new Way.Lib.NetStream(socket);
            Client.ReadTimeout = 0;
            var cmd = ReadCommand();

            _logger?.LogDebug("收到命令，type:{0} content:{1}" , cmd.Type , cmd.Content);

            switch (cmd.Type )
            {
                case CommandType.Register:
                    var serviceClient = _serviceProvider.GetService<ServiceClient>();
                    serviceClient._commandHandler = this;
                    serviceClient.Init(cmd);
                    return;
                case CommandType.GetServiceProvider:
                    if(cmd.Header.ContainsKey("TranId") == false)
                    {
                        cmd.Header["TranId"] = _TransactionIdBuilder.Build();
                    }
                    var requestBody = cmd.Content.FromJson<GetServiceProviderRequest>();
                    requestBody.Header = cmd.Header;
                    requestBody.ClientAddress = ((IPEndPoint)Client.Socket.RemoteEndPoint).Address.ToString();
                    try
                    {
                        var location = _serviceProvider.GetService<IServiceProviderAllocator>().Alloc(requestBody);
                        location.TransactionId = cmd.Header["TranId"];
                        this.Write(location);
                    }
                    catch
                    {
                        this.Write(new RegisterServiceLocation
                        {
                            Host = "",
                            Port = 0,
                            TransactionId = cmd.Header["TranId"]
                        });
                    }
                   
                    break;
                case CommandType.GetAllServiceProviders:
                    var locations = this.List(cmd.Content);
                    this.Write(locations);
                    break;
            }

            Client.Close();
        }

        public void Dispose()
        {
            Client?.Dispose();
            Client = null;
        }
    }
}
