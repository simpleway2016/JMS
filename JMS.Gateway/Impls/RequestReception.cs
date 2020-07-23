using JMS.Dtos;
using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Way.Lib;

namespace JMS.Impls
{
    class RequestReception : IRequestReception
    {
        ILogger<RequestReception> _logger;
        ICommandHandlerManager _manager;
        Gateway _gateway;
        public RequestReception(ILogger<RequestReception> logger, 
            Gateway gateway,
            ICommandHandlerManager manager)
        {
            _logger = logger;
            _manager = manager;
            _gateway = gateway;
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_gateway.AcceptCertHash != null && _gateway.AcceptCertHash.Length >0 && _gateway.AcceptCertHash.Contains(certificate.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }
        public void Interview(Socket socket)
        {
            try
            {
                using (var client = new NetClient(socket))
                {
                    if (_gateway.ServerCert != null)
                    {
                        var sslts = new SslStream(client.InnerStream , false , new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback));
                        sslts.AuthenticateAsServer(_gateway.ServerCert, true,  NetClient.SSLProtocols , true);
                        client.InnerStream = sslts;
                    }

                    while (true)
                    {
                        var cmd = client.ReadServiceObject<GatewayCommand>();
                        _logger?.LogDebug("type:{0} content:{1}", cmd.Type, cmd.Content);

                        _manager.AllocHandler(cmd)?.Handle(client, cmd);

                        if (client.HasSocketException || !client.KeepAlive)
                            break;
                    }
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
