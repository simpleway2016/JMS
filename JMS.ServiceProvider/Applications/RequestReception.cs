﻿using JMS.Domains;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using JMS.Dtos;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Security.Authentication;

namespace JMS.Applications
{
    class RequestReception : IRequestReception
    {
        IConnectionCounter _connectionCounter;
        IRequestHandler[] _cache;
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

            var handlers = _MicroServiceProvider.ServiceProvider.GetServices<IRequestHandler>();
            _cache = new IRequestHandler[ handlers.Max(m=>(int)m.MatchType) + 1 ];
            foreach (var handler in handlers)
            {
                _cache[(int)handler.MatchType] = handler;
            }

            CheckCert = new RemoteCertificateValidationCallback(remoteCertificateValidationCallback);
        }

        bool remoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_SSLConfiguration != null)
            {
                if (_SSLConfiguration.AcceptClientCertHash != null && _SSLConfiguration.AcceptClientCertHash.Length > 0
                    && _SSLConfiguration.AcceptClientCertHash.Contains(certificate?.GetCertHashString()) == false)
                {
                    return false;
                }
            }
            return true;
        }

        async Task<InvokeCommand> GetRequestCommand(NetClient client)
        {
            var ret = await client.PipeReader.ReadAtLeastAsync(4);
            if (ret.IsCompleted && ret.Buffer.Length < 4)
                throw new SocketException();

            var text = Encoding.UTF8.GetString(ret.Buffer.Slice(0, 4).First.Span);
            client.PipeReader.AdvanceTo(ret.Buffer.Start);//好像在做无用功，但是没有这句，有可能往下再读数据会卡住

            if (text == "GET " || text == "POST")
            {
                return new InvokeCommand { Type = (int)InvokeType.Http};
            }
            else
            {
                return await client.ReadServiceObjectAsync<InvokeCommand>();
            }
        }

        RemoteCertificateValidationCallback CheckCert;
        public async void Interview(Socket socket)
        {
            try
            {
                using (var netclient = new NetClient(socket))
                {
                    var isSsl = false;
                    if (_SSLConfiguration != null && _SSLConfiguration.ServerCertificate != null)
                    {
                        isSsl = true;
                        await netclient.AsSSLServerAsync(_SSLConfiguration.ServerCertificate,false, CheckCert, _SSLConfiguration.SslProtocol);
                    }

                    while (true)
                    {
                        if (isSsl)
                        {
                            _ = await netclient.BaseStream.ReadAsync(Memory<byte>.Empty);
                        }

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

                    //对于没有keep alive的连接，等待一会再释放会好一些，防止有些数据发出去对方收不到
                    await Task.Delay(2000);
                }
            }
            catch (WebSocketException)
            {

            }
            catch (SocketException)
            {

            }
            catch (AuthenticationException)
            {

            }
            catch (OperationCanceledException)
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
