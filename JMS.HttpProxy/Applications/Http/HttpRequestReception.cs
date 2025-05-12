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
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Net;
using System.Security.Authentication;
using JMS.HttpProxy.Servers;

namespace JMS.HttpProxy.Applications.Http
{
    class HttpRequestReception
    {
        HttpRequestHandler _httpRequestHandler;
        HttpServer _httpServer;
        ILogger<HttpRequestReception> _logger;

        public HttpRequestReception(ILogger<HttpRequestReception> logger,
            HttpRequestHandler httpRequestHandler)
        {
            _httpRequestHandler = httpRequestHandler;
            _logger = logger;
        }

        public void SetServer(HttpServer httpServer)
        {
            _httpServer = httpServer;
            _httpRequestHandler.SetServer(_httpServer);
        }

      

        public async void Interview(Socket socket)
        {
            try
            {
                using (var client = new NetClient(socket))
                {
                    var isSsl = false;
                    if (_httpServer.SslServerAuthenticationOptions != null)
                    {
                        isSsl = true;
                        await client.AsSSLServerAsync(_httpServer.SslServerAuthenticationOptions);

                    }

                    while (true)
                    {
                        if (isSsl)
                        {
                            _ = await client.BaseStream.ReadAsync(Memory<byte>.Empty, CancellationToken.None);
                        }

                        await _httpRequestHandler.Handle(client);

                        if (client.HasSocketException || !client.KeepAlive)
                            break;
                    }

                    //对于没有keep alive的连接，等待一会再释放会好一些，防止有些数据发出去对方收不到
                    await Task.Delay(2000);
                }
            }
            catch (System.Security.Authentication.AuthenticationException ex)
            {
                //ssl握手失败
                if (HttpProxyProgram.Config.Current.LogDetails)
                {
                    _logger?.LogError(ex, "");
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
