using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using JMS.Dtos;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace JMS.Impls
{
    class RequestReception : IRequestReception
    {
        Dictionary<InvokeType, IRequestHandler> _cache = new Dictionary<InvokeType, IRequestHandler>();
        MicroServiceHost _MicroServiceProvider;
        ILogger<RequestReception> _logger;
        ProcessExitHandler _processExitHandler;
        SSLConfiguration _SSLConfiguration;
        public RequestReception(ILogger<RequestReception> logger,
            ProcessExitHandler processExitHandler,
            MicroServiceHost microServiceProvider)
        {
            _logger = logger;
            _MicroServiceProvider = microServiceProvider;
            _processExitHandler = processExitHandler;
            _SSLConfiguration = _MicroServiceProvider.ServiceProvider.GetService<SSLConfiguration>();

            var handlerTypes = typeof(RequestReception).Assembly.DefinedTypes.Where(m => m.ImplementedInterfaces.Contains(typeof(IRequestHandler)));
            foreach( var type in handlerTypes )
            {
                var handler = (IRequestHandler)microServiceProvider.ServiceProvider.GetService(type);
                _cache[handler.MatchType] = handler;
            }
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_SSLConfiguration != null)
            {
                if (_SSLConfiguration.AcceptClientCertHash != null && _SSLConfiguration.AcceptClientCertHash.Length > 0
                    && _SSLConfiguration.AcceptClientCertHash.Contains(certificate.GetCertHashString()) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public void Interview(Socket socket)
        {
            try
            {                
                using (var netclient = new NetClient(socket))
                {
                    if (_SSLConfiguration != null && _SSLConfiguration.ServerCertificate != null)
                    {
                        var sslts = new SslStream(netclient.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback));
                        sslts.AuthenticateAsServer(_SSLConfiguration.ServerCertificate, true, NetClient.SSLProtocols, true);
                        netclient.InnerStream = sslts;
                    }

                    while (true)
                    {
                        var cmd = netclient.ReadServiceObject<InvokeCommand>();
                        if (_processExitHandler.ProcessExited)
                            return;

                        Interlocked.Increment(ref _MicroServiceProvider.ClientConnected);
                        try
                        {
                            _cache[cmd.Type].Handle(netclient, cmd);
                        }
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _MicroServiceProvider.ClientConnected);
                        }

                        if (netclient.HasSocketException || !netclient.KeepAlive)
                            break;
                    }
                }
            }
            catch (SocketException)
            {

            }
            catch (System.IO.IOException ex)
            {
                if (ex.HResult != -2146232800)
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
