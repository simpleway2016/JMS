using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    class Gateway
    {
        TcpListener _tcpListener;
        ILogger<Gateway> _Logger;
        internal IServiceProvider ServiceProvider { get; set; }
        public List<ServiceClient> ServiceClients { get; set; }
        public Gateway(ILogger<Gateway> logger)
        {
            _Logger = logger;
               ServiceClients = new List<ServiceClient>();
        }

        void onSocketConnect(Socket socket)
        {
            try
            {
                new CommandHandler(ServiceProvider).Handle(socket);
            }
            catch(Exception ex)
            {
                _Logger?.LogError(ex, ex.Message);
            }           
        }

        public void Run(int port)
        {
            _tcpListener = new TcpListener(IPAddress.Any, port);
            _tcpListener.Start();
            _Logger?.LogInformation("Gateway started, port:{0}", port);
            while (true)
            {
                try
                {
                    var socket = _tcpListener.AcceptSocket();
                    Task.Run(() => onSocketConnect(socket));
                }
                catch (Exception ex)
                {
                    _Logger?.LogError(ex, ex.Message);
                    break;
                }
               
            }
        }

        public RegisterServiceInfo[] GetAllServiceProviders()
        {
            List<RegisterServiceInfo> ret = new List<RegisterServiceInfo>();
            for(int i = 0; i < ServiceClients.Count; i ++)
            {
                var client = ServiceClients[i];
                if(client != null && ret.Contains(client.ServiceInfo) == false)
                {
                    ret.Add(client.ServiceInfo);
                }
            }
            return ret.ToArray();
        }
    }
}
