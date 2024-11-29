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

namespace JMS.HttpProxy.Applications.Sockets
{
    public class SocketRequestReception
    {
        SocketServer _socketServer;
        private readonly ILogger<SocketRequestReception> _logger;
        private readonly BlackList _blackList;
        private readonly SocketNetClientProvider _socketNetClientProvider;
        private readonly InternalConnectionProvider _connectionProvider;

        public SocketRequestReception(ILogger<SocketRequestReception> logger, BlackList blackList,
            SocketNetClientProvider socketNetClientProvider,
            InternalConnectionProvider connectionProvider)
        {
            _logger = logger;
            _blackList = blackList;
            _socketNetClientProvider = socketNetClientProvider;
            _connectionProvider = connectionProvider;
            _blackList.SetKeepMinutes(120);//设置黑名单时间为120分钟
        }

        public void SetServer(SocketServer server)
        {
            _socketServer = server;
        }

        public async void Interview(Socket socket)
        {
          
            try
            {
                using var client = new NetClient(socket);
                using var proxyClient = await _socketNetClientProvider.GetClientAsync(_socketServer.Config.Proxies[0].Target);

                client.ReadTimeout = 0;
                proxyClient.ReadTimeout = 0;

                _ = proxyClient.ReadAndSendForLoop(client);

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
