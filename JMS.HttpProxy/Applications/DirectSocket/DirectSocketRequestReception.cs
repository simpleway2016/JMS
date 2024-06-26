using JMS.Common;
using JMS.HttpProxy.InternalProtocol;
using JMS.HttpProxy.Servers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Applications.DirectSocket
{
    public class DirectSocketRequestReception
    {
        DirectSocketServer _socketServer;
        private readonly ILogger<DirectSocketRequestReception> _logger;

        public DirectSocketRequestReception(ILogger<DirectSocketRequestReception> logger)
        {
            _logger = logger;
        }

        public void SetServer(DirectSocketServer server)
        {
            _socketServer = server;
        }

        public async void Interview(Socket socket)
        {
          
            try
            {
                using var client = new NetClient(socket);
                using var proxyClient = new NetClient();

                var arr = _socketServer.Config.Proxies[0].Target.Split(':');
                var port = 80;
                if(arr.Length > 1)
                    port = Convert.ToInt32(arr[1]);

                await proxyClient.ConnectAsync(new NetAddress(arr[0] , port));

                client.ReadTimeout = 0;
                proxyClient.ReadTimeout = 0;

                proxyClient.ReadAndSendForLoop(client);

                await client.ReadAndSendForLoop(proxyClient);
            }
            catch (SocketException)
            {

            }
            catch (OperationCanceledException)
            {

            }
            catch (IOException ex)
            {
                if (ex.HResult != -2146232800)
                {
                    _logger?.LogError(ex, "");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "");
            }
           

        }
    }
}
