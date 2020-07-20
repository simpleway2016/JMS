using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Token
{
    public class TokenClient
    {
      
        static string[] Keys;
        static object LockObj = new object();
        X509Certificate2 _cert;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serverAddress">Token服务器地址</param>
        /// <param name="serverPort">Token服务器端口</param>
        /// <param name="cert">与服务器交互的客户端证书</param>
        public TokenClient(string serverAddress ,int serverPort, X509Certificate2 cert = null)
        {
            _cert = cert;
            if(Keys == null)
            {
                lock(LockObj)
                {
                    if (Keys != null)
                        return;

                    NetStream client = new NetStream(serverAddress, serverPort);
                    if (_cert != null)
                    {
                        SslStream sslStream = new SslStream(client.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), null);
                        X509CertificateCollection certs = new X509CertificateCollection();
                        certs.Add(cert);
                        sslStream.AuthenticateAsClient("SslSocket", certs, System.Security.Authentication.SslProtocols.Tls, true);
                        client.InnerStream = sslStream;
                    }
                    var len = client.ReadInt();
                    Keys = Encoding.UTF8.GetString(client.ReceiveDatas(len)).FromJson<string[]>();
                    Task.Run(() => {
                        try
                        {
                            client.ReadTimeout = 0;
                            client.ReadInt();
                        }
                        catch (Exception)
                        {
                            Keys = null;
                        }
                    });
                }               
            }
        }
        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
        /// <summary>
        /// 根据字符串内容，生成token
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        public string BuildForString(string body)
        {
            var signstr = sign(body);
            var text = new string[] { body, signstr }.ToJsonString();
            var bs = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bs);
        }

        /// <summary>
        /// 根据long数组，生成token，常用于存储用户id和过期时间戳
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public string BuildForLongs(long[] values)
        {
            var signstr = sign(values.ToJsonString());
            var signbs = Encoding.UTF8.GetBytes(signstr);
            byte[] data = new byte[values.Length * 8 + 2 + signbs.Length];
            Array.Copy(BitConverter.GetBytes((short)values.Length), data , 2);
            for (int i = 0; i < values.Length; i ++)
            {
                Array.Copy(BitConverter.GetBytes(values[i]), 0, data, i * 8 + 2, 8);
            }
            Array.Copy(signbs, 0, data, values.Length * 8 + 2, signbs.Length);
            return Convert.ToBase64String(data);
        }

        static string sign(string body)
        {
            var str = body;
            str += Keys[1];
            str = Way.Lib.AES.Encrypt(str, Keys[0]);
            return GetHash(str);
        }

        /// <summary>
        /// 验证token，如果正确，返回token的信息对象
        /// </summary>
        /// <param name="token"></param>
        /// <param name="key"></param>
        /// <param name="secretKey"></param>
        /// <returns></returns>
        public string VerifyForString(string token)
        {
           var tokenInfo =  Encoding.UTF8.GetString( Convert.FromBase64String(token)).FromJson<string[]>();
            var signstr = sign(tokenInfo[0]);
            if (signstr == tokenInfo[1])
                return tokenInfo[0];
            throw new Exception("Token验证失败");
        }

        /// <summary>
        /// 验证token，如果正确，返回long数组
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public long[] VerifyForLongs(string token)
        {
            var data = Convert.FromBase64String(token);
            var arrlen = BitConverter.ToInt16(data);

            long[] ret = new long[arrlen];
            for(int i = 0; i < ret.Length; i ++)
            {
                ret[i] = BitConverter.ToInt64(data, 2 + i * 8);
            }

            var input = Encoding.UTF8.GetString(data , 2 + ret.Length*8 , data.Length - 2 - ret.Length * 8);

            var signstr = sign(ret.ToJsonString());
            if (signstr == input)
                return ret;
            throw new Exception("Token验证失败");
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
