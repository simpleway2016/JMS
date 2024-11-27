using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

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
            return AllListeners.GetOrAdd(key, (k) => {
                var obj = new DisableTokenListener(serverAddr);
                obj.Start();
                return obj;
            });
        }

        public void AddDisableToken(string token,long expireTime)
        {
            _disableTokens.TryAdd(token, expireTime);
        }

        void Start()
        {
            if (string.IsNullOrEmpty(_netAddress.Address))
                return;

            bool printedErr = false;
            new Thread(()=> { 
                while(true)
                {
                    try
                    {
                        int len;
                        CertClient client = null;
                        try
                        {
                            TokenClient.Logger?.LogInformation("开始连接 token server ...");
                            client = new CertClient();
                            client.Connect(_netAddress);

                            TokenClient.Logger?.LogInformation("成功连接 token server");

                            client.Write(888);
                            len = client.ReadInt();
                            if (len != 4)
                            {
                                TokenClient.Logger?.LogError("Token Server请使用最新版本");
                                return;
                            }
                            else
                            {
                                var data = client.ReadInt();
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
                        client.Connect(_netAddress);
                        client.Write(999);
                        len = client.ReadInt();
                        var buffer = new byte[len];
                        client.ReadData(buffer, 0, len);
                        var keyvalue = Encoding.UTF8.GetString(buffer).FromJson<string[]>();
                        TokenClient.ServerKeys.AddOrUpdate((_netAddress.Address, _netAddress.Port), keyvalue, (k, o) => keyvalue);

                        printedErr = false;
                        client.ReadTimeout = 30000;
                        while(true)
                        {
                            var hasvalue = client.ReadBoolean();
                            if(hasvalue)
                            {
                                var expireTime = client.ReadLong();
                                len = client.ReadInt();
                                buffer = new byte[len];
                                client.ReadData(buffer, 0, buffer.Length);
                                var token = Encoding.UTF8.GetString(buffer);
                                _disableTokens.TryAdd(token, expireTime);
                                TokenClient.Logger?.LogInformation("Token:{0}被作废", token);
                            }
                            else
                            {
                                var utctime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                foreach ( var pair in _disableTokens )
                                {
                                    if( pair.Value != 0 && pair.Value <= utctime)
                                    {
                                        _disableTokens.TryRemove(pair.Key, out long o);
                                    }
                                }
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        if (!printedErr)
                        {
                            printedErr = true;
                            TokenClient.Logger?.LogError(ex, "与Token Server通讯发生异常");
                        }
                        Thread.Sleep(2000);
                    }
                }
            }).Start();
        }
    }
}
