﻿using JMS.Domains;
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
using System.Threading.Tasks;

namespace JMS.Applications
{
    class RequestReception : IRequestReception
    {
        IConnectionCounter _connectionCounter;
        Dictionary<InvokeType, IRequestHandler> _cache = new Dictionary<InvokeType, IRequestHandler>();
        MicroServiceHost _MicroServiceProvider;
        ILogger<RequestReception> _logger;
        IProcessExitHandler _processExitHandler;
        SSLConfiguration _SSLConfiguration;
        public RequestReception(ILogger<RequestReception> logger,
            IProcessExitHandler processExitHandler,
            IConnectionCounter connectionCounter,
            MicroServiceHost microServiceProvider)
        {
            this._connectionCounter = connectionCounter;
            _logger = logger;
            _MicroServiceProvider = microServiceProvider;
            _processExitHandler = processExitHandler;
            _SSLConfiguration = _MicroServiceProvider.ServiceProvider.GetService<SSLConfiguration>();

            var handlerTypes = typeof(RequestReception).Assembly.DefinedTypes.Where(m => m.ImplementedInterfaces.Contains(typeof(IRequestHandler)));
            foreach (var type in handlerTypes)
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

        async Task<InvokeCommand> GetRequestCommand(NetClient client)
        {
            byte[] data = new byte[4];
            await client.ReadDataAsync(data, 0, data.Length);

            var text = Encoding.UTF8.GetString(data);
            if (text == "GET " || text == "POST")
            {
                return new InvokeCommand { Type = InvokeType.Http, Service = text };
            }
            else
            {
                data = await client.ReadServiceDataBytesAsync(BitConverter.ToInt32(data));
                return Encoding.UTF8.GetString(data).FromJson<InvokeCommand>();
            }
        }

        public async void Interview(Socket socket)
        {
            try
            {
                using (var netclient = new NetClient(socket))
                {
                    if (_SSLConfiguration != null && _SSLConfiguration.ServerCertificate != null)
                    {
                        var sslts = new SslStream(netclient.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback));
                        await sslts.AuthenticateAsServerAsync(_SSLConfiguration.ServerCertificate, true, NetClient.SSLProtocols, false);
                        netclient.InnerStream = sslts;
                    }

                    while (true)
                    {
                        var cmd = await GetRequestCommand(netclient);
                        if (cmd == null)
                        {
                            netclient.Write(Encoding.UTF8.GetBytes("ok"));
                            return;
                        }
                        if (_processExitHandler.ProcessExited)
                            return;

                        _connectionCounter.OnConnect();
                        try
                        {
                            await _cache[cmd.Type].Handle(netclient, cmd);
                        }
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            _connectionCounter.OnDisconnect();
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
