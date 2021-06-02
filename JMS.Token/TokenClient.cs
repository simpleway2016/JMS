using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Token
{
    public class TokenClient
    {
        static Dictionary<(string, int), string[]> ServerKeys = new Dictionary<(string, int), string[]>();
        static object LockObj = new object();
        X509Certificate2 _cert;
        NetAddress _serverAddr;
        DisableTokenListener _DisableTokenListener;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serverAddress">Token服务器地址</param>
        /// <param name="serverPort">Token服务器端口</param>
        /// <param name="cert">与服务器交互的客户端证书</param>
        public TokenClient(string serverAddress, int serverPort, X509Certificate2 cert = null)
        {
            _cert = cert;
           
            _serverAddr = new NetAddress(serverAddress, serverPort);
            _DisableTokenListener = DisableTokenListener.Listen(serverAddress, serverPort, cert);
            var key = (_serverAddr.Address, _serverAddr.Port);
            if (ServerKeys.ContainsKey(key) == false)
            {
                getKeyFromServer(key);
            }

        }

        void getKeyFromServer((string addr,int port) key)
        {
            lock (LockObj)
            {
                if (ServerKeys.ContainsKey(key))
                    return;

                CertClient client = new CertClient(key.addr, key.port, _cert);
                client.Write(1);
                var len = client.ReadInt();
                ServerKeys[key] = Encoding.UTF8.GetString(client.ReceiveDatas(len)).FromJson<string[]>();
                Task.Run(() =>
                {
                    try
                    {
                        client.ReadTimeout = 0;
                        client.ReadInt();
                    }
                    catch (Exception)
                    {
                        ServerKeys.Remove(key);
                    }
                });
            }
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="expireTime">过期时间</param>
        /// <returns></returns>
        public string BuildLongWithExpire(long data, DateTime expireTime)
        {
            long time = (long)(expireTime - new DateTime(1970, 1, 1)).TotalSeconds;
            time = time * 1000 + new Random().Next(1, 999);
            return BuildForLongs(new long[] { data, time });
        }

        public string BuildStringWithExpire(string data, DateTime expireTime)
        {
            long time = (long)(expireTime - new DateTime(1970, 1, 1)).TotalSeconds;
            time = time * 1000 + new Random().Next(1, 999);

            var dict = new StringToken()
            {
                d = data,
                e = time
            };

            return BuildForString(dict.ToJsonString());
        }

        /// <summary>
        /// 验证Long类型的token，如果验证失败，抛出异常
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public long VerifyLong(string token)
        {
            if (!_DisableTokenListener.CheckToken(token))
            {
                throw new AuthenticationException("token is invalid");
            }
            var data = this.VerifyForLongs(token);
            if(data == null)
                throw new AuthenticationException("token is invalid");
            var expireTime = new DateTime(1970, 1, 1).AddMilliseconds(data[1]);
            if (expireTime < DateTime.Now)
                throw new AuthenticationException("token expired");
            return data[0];
        }

        /// <summary>
        /// 根据字符串内容，生成token
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        string BuildForString(string body)
        {
            var keys = ServerKeys[(_serverAddr.Address,_serverAddr.Port)];
            var signstr = sign(body , keys);
            var text = new string[] { body, signstr }.ToJsonString();
            var bs = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bs);
        }

        class StringToken
        {
            public string d;
            public long e;
        }

        /// <summary>
        /// 设置token为失效的
        /// </summary>
        /// <param name="token"></param>
        /// <param name="utcExpireTime">token原定的过期时间（utc时间）</param>
        public void SetTokenDisable(string token,DateTime? utcExpireTime)
        {
            long expireTime = 0;
            if(utcExpireTime != null)
            {
                expireTime = (long)(utcExpireTime.Value.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds + 1L;
            }

            CertClient client = new CertClient(_serverAddr, _cert);
            try
            {
                client.Write(2);
                client.Write(expireTime);
                var data = Encoding.UTF8.GetBytes(token);
                client.Write(data.Length);
                client.Write(data);
                try
                {
                    client.ReadBoolean();
                }
                catch
                {
                }
                _DisableTokenListener.AddDisableToken(token, expireTime);
            }
            finally
            {
                client.Dispose();
            }
        }

        /// <summary>
        /// 验证String类型的token，如果验证失败，抛出异常
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public string VerifyString(string token)
        {
            if (!_DisableTokenListener.CheckToken(token))
            {
                throw new AuthenticationException("token is invalid");
            }

            var ret = VerifyForString(token);
            if (ret == null)
            {
                throw new AuthenticationException("token is invalid");
            }
            var data = ret.FromJson<StringToken>();

            var expireTime = new DateTime(1970, 1, 1).AddMilliseconds(data.e);
            if (expireTime < DateTime.Now)
                throw new AuthenticationException("token expired");
            return data.d;
        }

        /// <summary>
        /// 根据long数组，生成token，常用于存储用户id和过期时间戳
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        string BuildForLongs(long[] values)
        {
            var keys = getKeys();
            var signstr = sign(values.ToJsonString() , keys);
            var signbs = Encoding.UTF8.GetBytes(signstr);
            byte[] data = new byte[values.Length * 8 + 2 + signbs.Length];
            Array.Copy(BitConverter.GetBytes((short)values.Length), data, 2);
            for (int i = 0; i < values.Length; i++)
            {
                Array.Copy(BitConverter.GetBytes(values[i]), 0, data, i * 8 + 2, 8);
            }
            Array.Copy(signbs, 0, data, values.Length * 8 + 2, signbs.Length);
            return Convert.ToBase64String(data);
        }

        static string sign(string body,string[] keys)
        {
            var str = body;
            str += keys[1];
            str = Way.Lib.AES.Encrypt(str, keys[0]);
            return GetHash(str);
        }

        /// <summary>
        /// 验证token，如果正确，返回token的信息对象
        /// </summary>
        /// <param name="token"></param>
        /// <param name="key"></param>
        /// <param name="secretKey"></param>
        /// <returns>验证成功返回字符串信息，失败返回null</returns>
        string VerifyForString(string token)
        {
            var keys = getKeys();
            var tokenInfo = Encoding.UTF8.GetString(Convert.FromBase64String(token)).FromJson<string[]>();
            var signstr = sign(tokenInfo[0] , keys);
            if (signstr == tokenInfo[1])
                return tokenInfo[0];
            return null;
        }

        /// <summary>
        /// 验证token，如果正确，返回long数组
        /// </summary>
        /// <param name="token"></param>
        /// <returns>验证成功返long数组，失败返回null</returns>
        long[] VerifyForLongs(string token)
        {
            var data = Convert.FromBase64String(token);
            var arrlen = BitConverter.ToInt16(data);

            long[] ret = new long[arrlen];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = BitConverter.ToInt64(data, 2 + i * 8);
            }

            var input = Encoding.UTF8.GetString(data, 2 + ret.Length * 8, data.Length - 2 - ret.Length * 8);
            var keys = getKeys();
            var signstr = sign(ret.ToJsonString() , keys);
            if (signstr == input)
                return ret;
            return null;
        }

        string[] getKeys()
        {
            while (true)
            {
                var key = (_serverAddr.Address, _serverAddr.Port);
                if (ServerKeys.TryGetValue(key, out string[] o))
                {
                    return o;
                }
                else
                {
                    getKeyFromServer(key);
                }
            }
        }

        static string GetHash(string content)
        {
            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.UTF8.GetBytes(content));
                var strResult = BitConverter.ToString(result);
                string result3 = strResult.Replace("-", "");
                return result3;
            }
        }
    }
}
