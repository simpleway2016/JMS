using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
namespace JMS.Impls
{
    class RequestReception : IRequestReception
    {
        MicroServiceProvider _MicroServiceProvider;
        ILogger<RequestReception> _logger;
        IInvokeRequestHandler _invokeRequestHandler;
        ICommitRequestHandler _commitTransactionHandler;
        public RequestReception(ILogger<RequestReception> logger, 
            ICommitRequestHandler commitTransactionHandler,
            IInvokeRequestHandler invokeRequestHandler,
            MicroServiceProvider microServiceProvider)
        {
            _logger = logger;
            _MicroServiceProvider = microServiceProvider;
            _invokeRequestHandler = invokeRequestHandler;
            _commitTransactionHandler = commitTransactionHandler;
        }
        public void Interview(Socket socket)
        {
            try
            {
                Interlocked.Increment(ref _MicroServiceProvider.ClientConnected);
                using (var netclient = new Way.Lib.NetStream(socket))
                {
                    var cmd = netclient.ReadServiceObject<InvokeCommand>();
                    switch(cmd.Type)
                    {
                        case InvokeType.Invoke:
                            _invokeRequestHandler.Handle(netclient, cmd);
                            break;
                        case InvokeType.CommitTranaction:
                            _commitTransactionHandler.Handle(netclient, cmd);
                            break;
                    }
                    
                }
            }
            catch (SocketException)
            {

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref _MicroServiceProvider.ClientConnected);
            }
        }
    }
}
