﻿using JMS.HttpProxy.Applications.DirectSocket;
using JMS.HttpProxy.Applications.Http;
using JMS.HttpProxy.Attributes;
using JMS.HttpProxy.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.Servers
{
    [ProxyType(ProxyType.DirectSocket)]
    public class DirectSocketServer : ProxyServer,IDisposable
    {
        DirectSocketRequestReception _requestReception;
        JMS.ServerCore.MulitTcpListener _tcpServer;
        ILogger<DirectSocketServer> _logger;

        public DirectSocketServer()
        {

        }

        public override void Init()
        {
            _logger = this.ServiceProvider.GetService<ILogger<DirectSocketServer>>();
            _requestReception = ServiceProvider.GetService<DirectSocketRequestReception>();
            _requestReception.SetServer(this);
            _tcpServer = new ServerCore.MulitTcpListener(this.Config.Port , null);
        }

        public override void Run()
        {
            _tcpServer.Connected += _tcpServer_Connected;
            _tcpServer.OnError += _tcpServer_OnError;
         

            _logger?.LogInformation($"Listening direct socket prot: {Config.Port}");
           
            _tcpServer.Run();
        }

        private void _tcpServer_OnError(object sender, Exception err)
        {
            _logger.LogError(err, "");
        }

        private void _tcpServer_Connected(object sender, System.Net.Sockets.Socket socket)
        {
            Task.Run(() => _requestReception.Interview(socket));
        }

        public void Dispose()
        {
            if (_tcpServer != null)
            {
                _tcpServer.Stop();
                _tcpServer = null;
            }
        }
    }
}
