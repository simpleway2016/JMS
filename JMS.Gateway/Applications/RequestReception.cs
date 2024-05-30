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
using JMS.Applications.CommandHandles;
using System.Threading.Tasks;
using System.Net;
using JMS.Common;
using System.Security.Authentication;

namespace JMS.Applications
{
    class RequestReception : IRequestReception
    {
        ILogger<RequestReception> _logger;
        private readonly ISslConfiguration _sslConfiguration;
        ICommandHandlerRoute _manager;
        public RequestReception(ILogger<RequestReception> logger,
            ISslConfiguration sslConfiguration,
            ICommandHandlerRoute manager)
        {
            _logger = logger;
            _sslConfiguration = sslConfiguration;
            _manager = manager;
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_sslConfiguration.AcceptCertHash != null && _sslConfiguration.AcceptCertHash.Length >0 && _sslConfiguration.AcceptCertHash.Contains(certificate?.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }

        async Task<GatewayCommand> GetRequestCommand(NetClient client)
        {
            var ret = await client.PipeReader.ReadAtLeastAsync(4);
            if (ret.IsCompleted && ret.Buffer.Length < 4)
                throw new SocketException();

            var text = Encoding.UTF8.GetString(ret.Buffer.Slice(0,4));
            client.PipeReader.AdvanceTo(ret.Buffer.Start);//好像在做无用功，但是没有这句，有可能往下再读数据会卡住

            if ( text == "GET " || text == "POST" || text == "PUT " || text == "OPTI")
            {
                return new GatewayCommand { Type = (int)CommandType.HttpRequest};
            }
            else
            {
               return await client.ReadServiceObjectAsync<GatewayCommand>();
            }    
        }

        public async void Interview(Socket socket)
        {
            try
            {
                using (var client = new NetClient(socket))
                {
                    if (_sslConfiguration.ServerCert != null)
                    {
                        await client.AsSSLServerAsync(_sslConfiguration.ServerCert, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), NetClient.SSLProtocols);

                    }

                    while (true)
                    {
                        var cmd = await GetRequestCommand(client);
                        if (cmd == null)
                        {
                            client.Write(Encoding.UTF8.GetBytes("ok"));
                            return;
                        }
                        _logger?.LogTrace("type:{0} content:{1}", cmd.Type, cmd.Content);

                        await _manager.AllocHandler(cmd)?.Handle(client, cmd);

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
            catch (AuthenticationException)
            {

            }
            catch (OperationCanceledException)
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
