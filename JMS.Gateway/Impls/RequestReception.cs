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
using System.Web;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;

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

        GatewayCommand GetRequestCommand(NetClient client)
        {
            // var cmd = client.ReadServiceObject<GatewayCommand>();
            byte[] data = new byte[3];
            int readed = client.Socket.Receive(data,data.Length , SocketFlags.Peek);
            if (readed < 3)
                return null;

            var text = Encoding.UTF8.GetString(data);
            if( text == "GET" || text == "POS")
            {
                List<byte> buffer = null;
                data = new byte[1024];
                string questLine = null;

                while (true)
                {
                    readed = client.Socket.Receive(data , data.Length , SocketFlags.None);
                    if (readed <= 0)
                        return null;
                 
                    if (data.Any(m => m == 10))
                    {
                        if( buffer != null )
                        {
                            buffer.AddRange(data);
                            data = buffer.ToArray();
                        }
                        questLine = Encoding.UTF8.GetString(data);
                        questLine = questLine.Substring(0, questLine.IndexOf("\r"));
                        break;
                    }
                    else
                    {
                        if(buffer == null)
                        {
                            buffer = new List<byte>();
                        }                       
                        buffer.AddRange(data);
                    }
                }

                var httpRequest = questLine.Split(' ')[1];
                if(httpRequest.StartsWith("/?GetServiceProvider=") || httpRequest.StartsWith("/?GetAllServiceProviders"))
                {
                    httpRequest = httpRequest.Substring(2);
                    if(httpRequest.Contains("="))
                    {
                        var arr = httpRequest.Split('=');
                        return new GatewayCommand
                        {
                            Type = (CommandType)Enum.Parse(typeof(CommandType), arr[0]),
                            Content = HttpUtility.UrlDecode( arr[1] , Encoding.UTF8),
                            IsHttp = true
                        };
                    }
                    else
                    {
                        return new GatewayCommand {
                            Type = (CommandType)Enum.Parse(typeof(CommandType), httpRequest),
                            IsHttp = true
                        };
                    }
                }
                else
                {
                    //重定向
                    var location = _gateway.ServiceProvider.GetService<IServiceProviderAllocator>().Alloc(new GetServiceProviderRequest { 
                        ServiceName = "WebServer"
                    });
                    if (location != null && !string.IsNullOrEmpty(location.ServiceAddress))
                    {
                        var serverAddr = location.ServiceAddress;
                        if (serverAddr.EndsWith("/"))
                            serverAddr = serverAddr.Substring(0, serverAddr.Length);                        

                        client.OutputHttpRedirect($"{serverAddr}{httpRequest}");
                    }
                    else
                    {
                        client.OutputHttpNotFund();
                    }
                }
                return null;
            }
            else
            {
                return client.ReadServiceObject<GatewayCommand>();
            }    
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
                        sslts.AuthenticateAsServer(_gateway.ServerCert, true,  NetClient.SSLProtocols , false);
                        client.InnerStream = sslts;
                    }

                    while (true)
                    {
                        var cmd = GetRequestCommand(client);
                        if (cmd == null)
                        {
                            client.Write(Encoding.UTF8.GetBytes("ok"));
                            return;
                        }
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
