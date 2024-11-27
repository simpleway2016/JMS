﻿using JMS.Common.Security;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace JMS.Token
{
    public class TokenClient
    {
        NetAddress _serverAddress;
        internal static ConcurrentDictionary<(string, int), string[]> ServerKeys = new ConcurrentDictionary<(string, int), string[]>();
        DisableTokenListener _DisableTokenListener;
        public static ILogger<TokenClient> Logger;
        static Random RandomObj = new Random();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serverAddress">Token服务器地址,null表示不需要服务器，本地自行验证</param>
        public TokenClient(NetAddress serverAddress)
        {
            if (serverAddress == null)
            {
                serverAddress = new NetAddress(null, 0);
            }
            this._serverAddress = serverAddress;

            _DisableTokenListener = DisableTokenListener.Listen(_serverAddress);
            var key = (serverAddress.Address, serverAddress.Port);
            if (ServerKeys.ContainsKey(key) == false)
            {
                getKeyFromServer(key);
            }

        }

        static string GetRandomString(int length)
        {
            byte[] b = new byte[4];
            new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(b);
            Random r = new Random(BitConverter.ToInt32(b, 0));
            string s = null, str = "";
            str += "0123456789";
            str += "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            str += "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
            for (int i = 0; i < length; i++)
            {
                s += str.Substring(r.Next(0, str.Length - 1), 1);
            }
            return s;
        }

        static Random _random = new Random();
        void getKeyFromServer((string addr, int port) key)
        {
            string[] value;
            if (string.IsNullOrEmpty(_serverAddress.Address))
            {
                value = new string[2];
                value[0] = GetRandomString(32);
                value[1] = GetRandomString(_random.Next(36, 66));
            }
            else
            {
                CertClient client = new CertClient();
                client.Connect(_serverAddress);
                client.Write(1);
                var len = client.ReadInt();
                var data = new byte[len];
                client.ReadData(data, 0, len);
                value = Encoding.UTF8.GetString(data).FromJson<string[]>();
            }
            ServerKeys.AddOrUpdate(key, value, (k, old) => value);
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }


        public string Build(string data, DateTime expireTime)
        {
            expireTime = expireTime.ToUniversalTime();
            long time = (long)(expireTime - new DateTime(1970, 1, 1)).TotalSeconds;

            var dict = new StringToken()
            {
                d = data,
                e = time
            };

            return BuildForString(dict.ToJsonString());
        }

        public string Build(string data, string role, DateTime expireTime)
        {
            expireTime = expireTime.ToUniversalTime();
            long time = (long)(expireTime - new DateTime(1970, 1, 1)).TotalSeconds;

            var dict = new StringToken()
            {
                d = data,
                e = time,
                r = role
            };

            return BuildForString(dict.ToJsonString());
        }

        public string Build(string data, string[] roles, DateTime expireTime)
        {
            expireTime = expireTime.ToUniversalTime();
            long time = (long)(expireTime - new DateTime(1970, 1, 1)).TotalSeconds;

            var dict = new StringToken()
            {
                d = data,
                e = time,
                rs = roles
            };

            return BuildForString(dict.ToJsonString());
        }

        /// <summary>
        /// 验证Long类型的token，如果验证失败，抛出异常
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        long VerifyLong(string token)
        {

            var data = this.ParseLongs(token);
            if (data == null)
                throw new AuthenticationException("token is invalid");
            var expireTime = new DateTime(1970, 1, 1).AddSeconds(data[1]);
            if (expireTime < DateTime.Now.ToUniversalTime())
                throw new AuthenticationException("token expired");
            return data[0];
        }

        /// <summary>
        /// 验证String类型的token，如果验证失败，抛出异常
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        ClaimsPrincipal VerifyString(string token)
        {

            var ret = ParseString(token);
            if (ret == null)
            {
                throw new AuthenticationException("token is invalid");
            }
            var data = ret.FromJson<StringToken>();

            var expireTime = new DateTime(1970, 1, 1).AddSeconds(data.e);
            if (expireTime < DateTime.Now.ToUniversalTime())
                throw new AuthenticationException("token expired");

            var claimsIdentity = new ClaimsIdentity(new Claim[]
          {
                new Claim("Content", data.d),
               
          }, "JMS.Token"); 

            if(data.r != null)
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, data.r));
            }
            if (data.rs != null)
            {
                foreach (var role in data.rs)
                {
                    claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }

            var principal = new ClaimsPrincipal(claimsIdentity);

            return principal;
        }

        /// <summary>
        /// 验证token
        /// </summary>
        /// <param name="token"></param>
        /// <returns>返回token内容</returns>
        public ClaimsPrincipal Verify(string token)
        {
            if (!_DisableTokenListener.CheckToken(token))
            {
                throw new AuthenticationException("token is invalid");
            }

            return VerifyString(token);
        }

        /// <summary>
        /// 根据字符串内容，生成token
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        string BuildForString(string body)
        {
            var keys = ServerKeys[(_serverAddress.Address, _serverAddress.Port)];
            var signstr = sign(body, keys);
            var text = new string[] { body, signstr }.ToJsonString(false);
            var bs = Encoding.UTF8.GetBytes(text);
            var token = Convert.ToBase64String(bs);
            return token;
        }

        class StringToken
        {
            public string d;
            public long e;
            public string r;
            public string[] rs;
        }

        /// <summary>
        /// 设置token为失效的
        /// </summary>
        /// <param name="token"></param>
        public void SetTokenDisable(string token)
        {
            long expireTime = 0;
            var ret = ParseString(token);
            if (ret == null)
            {
                return;
            }
            var tokendata = ret.FromJson<StringToken>();

            expireTime = tokendata.e;

            CertClient client = new CertClient();
            try
            {
                if (_serverAddress.Address != null)
                {
                    client.Connect(_serverAddress);
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
                }
                _DisableTokenListener.AddDisableToken(token, expireTime);
            }
            finally
            {
                client.Dispose();
            }
        }



        /// <summary>
        /// 根据long数组，生成token，常用于存储用户id和过期时间戳
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        string BuildForLongs(long[] values)
        {
            var keys = getKeys();
            var signstr = sign(values.ToJsonString(), keys);
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

        static string sign(string body, string[] keys)
        {
            var str = body;
            str += keys[1];
            str = AES.Encrypt(str, keys[0]);
            return GetHash(str);
        }

        /// <summary>
        /// 验证token，如果正确，返回token的信息对象
        /// </summary>
        /// <param name="token"></param>
        /// <param name="key"></param>
        /// <param name="secretKey"></param>
        /// <returns>验证成功返回字符串信息，失败返回null</returns>
        string ParseString(string token)
        {
            var keys = getKeys();
            if (keys == null)
                return null;

            var tokenInfo = Encoding.UTF8.GetString(Convert.FromBase64String(token)).FromJson<string[]>();
            var signstr = sign(tokenInfo[0], keys);
            if (signstr == tokenInfo[1])
                return tokenInfo[0];
            return null;
        }

        /// <summary>
        /// 验证token，如果正确，返回long数组
        /// </summary>
        /// <param name="token"></param>
        /// <returns>验证成功返long数组，失败返回null</returns>
        long[] ParseLongs(string token)
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
            var signstr = sign(ret.ToJsonString(), keys);
            if (signstr == input)
                return ret;
            return null;
        }

        string[] getKeys()
        {
            while (true)
            {
                var key = (_serverAddress.Address, _serverAddress.Port);
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
