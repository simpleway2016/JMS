using JMS.Dtos;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Net;
using System.Security.Authentication;
using JMS.HttpProxy.Servers;

namespace JMS.HttpProxy.Applications.StaticFiles
{
    class StaticFilesRequestReception
    {
        StaticFilesRequestHandler _requestHandler;
        StaticFilesServer _Server;
        ILogger<StaticFilesRequestReception> _logger;

        public StaticFilesRequestReception(ILogger<StaticFilesRequestReception> logger,
            StaticFilesRequestHandler requestHandler)
        {
            _requestHandler = requestHandler;
            _logger = logger;
        }

        public void SetServer(StaticFilesServer server)
        {
            _Server = server;
            _requestHandler.SetServer(_Server);
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }


        public async void Interview(Socket socket)
        {
            try
            {
                using (var client = new NetClient(socket))
                {
                    if (_Server.Certificate != null)
                    {                        
                        await client.AsSSLServerAsync(_Server.Certificate, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), _Server.Config.SSL.SslProtocol);

                    }

                    while (true)
                    {
                        await _requestHandler.Handle(client);

                        if (client.HasSocketException || !client.KeepAlive)
                            break;
                    }

                    //对于没有keep alive的连接，等待一会再释放会好一些，防止有些数据发出去对方收不到
                    await Task.Delay(2000);
                }
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
