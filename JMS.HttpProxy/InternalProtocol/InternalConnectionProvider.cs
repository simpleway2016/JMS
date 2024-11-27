using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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

            //检查状态
            checkStatus(name ,client);

            if (HttpProxyProgram.Config.Current.LogDetails)
            {
                _logger.LogInformation($"{name}当前可用连接数={queue.Count}");
            }
        }

        static byte[] CheckBs = new byte[1];
        /// <summary>
        /// 检查健康状态
        /// </summary>
        /// <param name="client"></param>
        /// <param name="queue"></param>
        async void checkStatus(string name, NetClient client)
        {
            try
            {
                var count = await client.Socket.ReceiveAsync(CheckBs, SocketFlags.Peek);
                if (count == 0)
                {
                    client.Dispose();
                    if (HttpProxyProgram.Config.Current.LogDetails)
                    {
                        _logger.LogInformation($"{name}连接断开");
                    }
                }
            }
            catch (Exception ex)
            {
                client.Dispose();
                if (HttpProxyProgram.Config.Current.LogDetails)
                {
                    _logger.LogInformation($"{name}连接异常断开");
                }
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
                        if (ret.Socket == null)
                        {
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
