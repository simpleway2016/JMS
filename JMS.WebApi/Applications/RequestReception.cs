using JMS.Dtos;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using JMS.Applications.CommandHandles;
using System.Threading.Tasks;
using System.Net;
using JMS.Common;
using System.Security.Authentication;
using static System.Net.Mime.MediaTypeNames;

namespace JMS.Applications
{
    class RequestReception : IRequestReception
    {
        HttpRequestHandler _httpRequestHandler;
        ILogger<RequestReception> _logger;
        private readonly IWebApiHostEnvironment _webApiEnvironment;

        public RequestReception(ILogger<RequestReception> logger, IWebApiHostEnvironment webApiEnvironment,
            HttpRequestHandler httpRequestHandler)
        {
            this._httpRequestHandler = httpRequestHandler;
            _logger = logger;
            _webApiEnvironment = webApiEnvironment;
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_webApiEnvironment.AcceptCertHash != null && _webApiEnvironment.AcceptCertHash.Length >0 && _webApiEnvironment.AcceptCertHash.Contains(certificate?.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }

        async Task<GatewayCommand> GetRequestCommand(NetClient client)
        {
            return new GatewayCommand { Type = (int)CommandType.HttpRequest };
        }

        public async void Interview(Socket socket)
        {
            try
            {
                using (var client = new NetClient(socket))
                {
                    bool redirectHttps = false;
                    if (_webApiEnvironment.ServerCert != null)
                    {
                        var text = await client.PreReadBytesAsync(4);
                        if (text == null)
                            return;

                        if (text == "GET " || text == "POST" || text == "PUT " || text == "OPTI" || text == "DELE")
                        {
                            redirectHttps = true;
                        }
                        else
                        {
                            await client.AsSSLServerAsync(_webApiEnvironment.ServerCert, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), _webApiEnvironment.SslProtocol);
                        }
                    }

                    while (true)
                    {
                        var cmd = await GetRequestCommand(client);

                        await _httpRequestHandler.Handle(client, cmd, redirectHttps);

                        if (client.HasSocketException || !client.KeepAlive)
                            break;
                    }

                    //对于没有keep alive的连接，等待一会再释放会好一些，防止有些数据发出去对方收不到
                    await Task.Delay(2000);
                }
            }
            catch(SocketException)
            {

            }
            catch (OperationCanceledException)
            {

            }
            catch (AuthenticationException)
            {

            }
            catch (SizeLimitException)
            {

            }
            catch (System.IO.IOException ex)
            {
                if(ex.HResult != -2146232800)
                {
                    _logger?.LogError(ex, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
            }

           
        }
    }
}
