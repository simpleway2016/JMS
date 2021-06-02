using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace JMS.Token
{
    class DisableTokenListener
    {
        static ConcurrentDictionary<(string, int), DisableTokenListener> AllListeners = new ConcurrentDictionary<(string, int), DisableTokenListener>();
        string _serverAddress;
        int _serverPort;
        X509Certificate2 _cert;
        ConcurrentDictionary<string, long> _disableTokens = new ConcurrentDictionary<string, long>();
        private DisableTokenListener( string serverAddress, int serverPort, X509Certificate2 cert)
        {
            this._serverAddress = serverAddress;
            this._serverPort = serverPort;
            _cert = cert;
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

        public static DisableTokenListener Listen(string serverAddress, int serverPort, X509Certificate2 cert)
        {
            var key = (serverAddress, serverPort);
            return AllListeners.GetOrAdd(key, (k) => {
                var obj = new DisableTokenListener(serverAddress, serverPort, cert);
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
            new Thread(()=> { 
                while(true)
                {
                    try
                    {
                        CertClient client = new CertClient(_serverAddress, _serverPort, _cert);
                        try
                        {
                            client.Write(998);
                            var len = client.ReadInt();
                            if (len != 4)
                            {
                                return;
                            }
                            else
                            {
                                var data = client.ReadInt();
                                if (data != 888)
                                {
                                    return;
                                }
                            }
                        }
                        finally
                        {
                            client.Dispose();
                        }
                        

                        client = new CertClient(_serverAddress,_serverPort, _cert);
                        client.Write(999);
                        client.ReadTimeout = 30000;
                        while(true)
                        {
                            var hasvalue = client.ReadBoolean();
                            if(hasvalue)
                            {
                                var expireTime = client.ReadLong();
                                var len = client.ReadInt();
                                var token = Encoding.UTF8.GetString(client.ReceiveDatas(len));
                                _disableTokens.TryAdd(token, expireTime);
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
                    catch
                    {
                        Thread.Sleep(2000);
                    }
                }
            }).Start();
        }
    }
}
