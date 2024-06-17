using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.InternalProtocol
{
    public class InternalConnectionProvider
    {
        private readonly ILogger<InternalConnectionProvider> _logger;
        ConcurrentDictionary<string, ConcurrentQueue<NetClient>> _connections = new ConcurrentDictionary<string, ConcurrentQueue<NetClient>>();

        public InternalConnectionProvider(ILogger<InternalConnectionProvider> logger)
        {
            _logger = logger;
        }

        public void AddConnection(string name, NetClient client)
        {
            client.KeepAlive = true;
            if (_connections.TryGetValue(name, out ConcurrentQueue<NetClient> queue) == false)
            {
                queue = _connections.GetOrAdd(name, s => new ConcurrentQueue<NetClient>());
            }

            queue.Enqueue(client);

            if (HttpProxyProgram.Config.Current.LogDetails)
            {
                _logger.LogInformation($"{name}当前可用连接数={queue.Count}");
            }
        }

        /// <summary>
        /// 取出一个连接实例
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async ValueTask<NetClient> GetConnectionAsync(string name)
        {
            for(int i = 0; i < 16; i ++) //最多8秒时间，超过就抛出超时异常
            {
                if (_connections.TryGetValue(name, out ConcurrentQueue<NetClient> queue))
                {
                    if (queue.TryDequeue(out NetClient ret))
                    {
                        try
                        {
                            ret.Socket.Send(new byte[0]);
                        }
                        catch (SocketException)
                        {
                            if (HttpProxyProgram.Config.Current.LogDetails)
                            {
                                _logger.LogInformation($"找到一个废弃的NetClient");
                            }
                            i--;
                            //连接已断开
                            ret.Dispose();
                            continue;
                        }

                        return ret;
                    }
                }

                await Task.Delay(500);
            }

            throw new TimeoutException($"{name}没有可用的链接");
        }
    }
}
