using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace JMS.TokenServer
{
    class ClientManager
    {
        ConcurrentDictionary<string, long> _disabledTokens = new ConcurrentDictionary<string, long>();
        ConcurrentDictionary<ClientReception, bool> _clients = new ConcurrentDictionary<ClientReception, bool>();
        public ClientManager()
        {
            deleteTokenAsync();
        }

        async void deleteTokenAsync()
        {
            while (true)
            {
                await Task.Delay(100000);
                var utctime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var pair in _disabledTokens)
                {
                    if (pair.Value != 0 && pair.Value <= utctime)
                    {
                        _disabledTokens.TryRemove(pair.Key, out long o);
                    }
                }
            }
        }

        public ClientReception AddClient(NetClient netStream)
        {
            var client = new ClientReception(netStream);
            _clients.TryAdd(client, true);

            //告诉客户端哪些key已经被禁用
            foreach( var pair in _disabledTokens )
            {
                client.OnTokenDisabled(pair.Key,pair.Value); 
            }

            return client;
        }

        public void DisableToken(string token, long utcExpireTime)
        {
            _disabledTokens.TryAdd(token, utcExpireTime);
            foreach ( var pair in _clients )
            {
                try
                {
                    pair.Key.OnTokenDisabled(token, utcExpireTime);
                }
                catch (Exception)
                {
                    _clients.TryRemove(pair.Key, out bool o);
                }
            }
        }
    }
}
