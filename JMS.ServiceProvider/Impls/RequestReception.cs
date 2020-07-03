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

namespace JMS.Impls
{
    class RequestReception : IRequestReception
    {
        Dictionary<InvokeType, IRequestHandler> _cache = new Dictionary<InvokeType, IRequestHandler>();
        MicroServiceProvider _MicroServiceProvider;
        ILogger<RequestReception> _logger;
        public RequestReception(ILogger<RequestReception> logger, 
            MicroServiceProvider microServiceProvider)
        {
            _logger = logger;
            _MicroServiceProvider = microServiceProvider;

            var handlerTypes = typeof(RequestReception).Assembly.DefinedTypes.Where(m => m.ImplementedInterfaces.Contains(typeof(IRequestHandler)));
            foreach( var type in handlerTypes )
            {
                var handler = (IRequestHandler)microServiceProvider.ServiceProvider.GetService(type);
                _cache[handler.MatchType] = handler;
            }
        }
        public void Interview(Socket socket)
        {
            try
            {
                Interlocked.Increment(ref _MicroServiceProvider.ClientConnected);
                using (var netclient = new Way.Lib.NetStream(socket))
                {
                    var cmd = netclient.ReadServiceObject<InvokeCommand>();
                    _cache[cmd.Type].Handle(netclient, cmd);
                    
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
