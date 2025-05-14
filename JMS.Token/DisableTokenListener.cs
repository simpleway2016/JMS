using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace JMS.Token
{
    class DisableTokenListener
    {
        static ConcurrentDictionary<(string, int), DisableTokenListener> AllListeners = new ConcurrentDictionary<(string, int), DisableTokenListener>();
        NetAddress _netAddress;
        X509Certificate2 _cert;
        ConcurrentDictionary<string, long> _disableTokens = new ConcurrentDictionary<string, long>();
        private DisableTokenListener(NetAddress serverAddress)
        {
            _netAddress = serverAddress;
        }

        /// <summary>
        /// 检查token是否有效
        /// </summary>
        /// <param name="token"></param>
        /// <returns>返回false，表示token失效</returns>
        public bool CheckToken(string token)
        {
            if (_disableTokens.ContainsKey(token))
                return false;
            return true;
        }

        public static DisableTokenListener Listen(NetAddress serverAddr)
        {
            var key = (serverAddr.Address, serverAddr.Port);
            var listener = AllListeners.GetOrAdd(key, (k) => {
                var obj = new DisableTokenListener(serverAddr);
               
                return obj;
            });

            listener.StartAsync();

            return listener;
        }

        public void AddDisableToken(string token,long expireTime)
        {
            _disableTokens.TryAdd(token, expireTime);
        }

        bool _started;
        async void StartAsync()
        {
            if (_started)
                return;

            if (string.IsNullOrEmpty(_netAddress.Address))
                return;


            _started = true;
            bool printedErr = false;
            while (true)
            {
                try
                {
                    int len;
                    CertClient client = null;
                    try
                    {
                        TokenClient.Logger?.LogInformation("开始连接 token server ...");
                        client = new CertClient();
                        await client.ConnectAsync(_netAddress);

                        TokenClient.Logger?.LogInformation("成功连接 token server");

                        client.Write(888);
                        len = await client.ReadIntAsync();
                        if (len != 4)
                        {
                            TokenClient.Logger?.LogError("Token Server请使用最新版本");
                            return;
                        }
                        else
                        {
                            var data = await client.ReadIntAsync();
                            if (data != 888)
                            {
                                TokenClient.Logger?.LogError("Token Server请使用最新版本");
                                return;
                            }
                        }
                    }
                    finally
                    {
                        client?.Dispose();
                    }


                    client = new CertClient();
                    await client.ConnectAsync(_netAddress);
                    client.Write(999);
                    len = await client.ReadIntAsync();
                    var buffer = new byte[len];
                    await client.ReadDataAsync(buffer, 0, len);
                    var keyvalue = Encoding.UTF8.GetString(buffer).FromJson<string[]>();
                    TokenClient.ServerKeys.AddOrUpdate((_netAddress.Address, _netAddress.Port), keyvalue, (k, o) => keyvalue);

                    printedErr = false;
                    client.ReadTimeout = 30000;
                    while (true)
                    {
                        var hasvalue = await client.ReadBooleanAsync();
                        if (hasvalue)
                        {
                            var expireTime = await client.ReadLongAsync();
                            len = await client.ReadIntAsync();
                            buffer = new byte[len];
                            await client.ReadDataAsync(buffer, 0, buffer.Length);
                            var token = Encoding.UTF8.GetString(buffer);
                            _disableTokens.TryAdd(token, expireTime);
                            TokenClient.Logger?.LogInformation("Token:{0}被作废", token);
                        }
                        else
                        {
                            var utctime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            foreach (var pair in _disableTokens)
                            {
                                if (pair.Value != 0 && pair.Value <= utctime)
                                {
                                    _disableTokens.TryRemove(pair.Key, out long o);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!printedErr)
                    {
                        printedErr = true;
                        TokenClient.Logger?.LogError(ex, "与Token Server通讯发生异常");
                    }
                    await Task.Delay(2000);
                }
            }
        }
    }
}
