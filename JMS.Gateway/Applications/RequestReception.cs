using JMS.Dtos;
using JMS.Domains;
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

namespace JMS.Applications
{
    class RequestReception : IRequestReception
    {
        ILogger<RequestReception> _logger;
        ICommandHandlerRoute _manager;
        Gateway _gateway;
        public RequestReception(ILogger<RequestReception> logger,
            Gateway gateway,
            ICommandHandlerRoute manager)
        {
            _logger = logger;
            _manager = manager;
            _gateway = gateway;
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_gateway.AcceptCertHash != null && _gateway.AcceptCertHash.Length >0 && _gateway.AcceptCertHash.Contains(certificate?.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }

        async Task<GatewayCommand> GetRequestCommand(NetClient client)
        {
            byte[] data = new byte[4];
            await client.ReadDataAsync(data, 0, data.Length);

            var text = Encoding.UTF8.GetString(data);
            if( text == "GET " || text == "POST" || text == "PUT " || text == "OPTI")
            {
                return new GatewayCommand { Type = CommandType.HttpRequest , Content = text };
            }
            else
            {
                var len = BitConverter.ToInt32(data);
                data = await client.ReadServiceDataBytesAsync(len);
                return Encoding.UTF8.GetString(data).FromJson<GatewayCommand>();
            }    
        }

        public async void Interview(Socket socket)
        {
            try
            {
                using (var client = new NetClient(socket))
                {
                    if (_gateway.ServerCert != null)
                    {
                        await client.AsSSLServerAsync(_gateway.ServerCert, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), NetClient.SSLProtocols);

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
