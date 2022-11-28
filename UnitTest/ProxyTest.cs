using Extreme.Net.Core.Proxy;
using Extreme.Net.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;

namespace UnitTest
{
    [TestClass]
    public class ProxyTest
    {
        static ProxyTest()
        {
            Task.Run(() => {
                JMS.Proxy.Program.Main(new string[] { "8918" });
            });
            Thread.Sleep(1000);
        }

        [TestMethod]
        public void Socks5ProxyClientTest()
        {

            var socksProxy = new Socks5ProxyClient("127.0.0.1", 8918);
            var handler = new ProxyHandler(socksProxy);

            HttpClient client = new HttpClient(handler);
            var ret = client.GetStringAsync("https://mail.qq.com/").ConfigureAwait(false).GetAwaiter().GetResult();
            client.Dispose();
        }

        [TestMethod]
        public void ProxyClientTest()
        {
            JMS.ProxyClient client = new JMS.ProxyClient(new JMS.NetAddress("127.0.0.1", 8918), null);
            client.Connect(new JMS.NetAddress("mail.qq.com", 443));
            client.AsSSLClient("mail.qq.com" , RemoteCertificateValidationCallback);

            var content = @"GET / HTTP/1.1
Host: mail.qq.com
Connection: keep-alive
User-Agent: JmsInvoker
Accept: text/html
Accept-Encoding: deflate, br
Accept-Language: zh-CN,zh;q=0.9
Content-Length: 0

";
            client.Write(Encoding.UTF8.GetBytes(content));

            byte[] data = new byte[40960];
            var len = client.InnerStream.Read(data , 0 , data.Length);
            var text = Encoding.UTF8.GetString(data, 0,len);
            client.Dispose();
        }

        [TestMethod]
        public void ProxyClientIPTest()
        {
            var ip = Dns.GetHostAddresses("mail.qq.com")[0].ToString();

            JMS.ProxyClient client = new JMS.ProxyClient(new JMS.NetAddress("127.0.0.1", 8918), null);
            client.Connect(new JMS.NetAddress(ip, 443));
            client.AsSSLClient("mail.qq.com", RemoteCertificateValidationCallback);
            for (int i = 0; i < 2; i++)
            {
                var content = @"GET / HTTP/1.1
Host: mail.qq.com
Connection: keep-alive
User-Agent: JmsInvoker
Accept: text/html
Accept-Encoding: deflate, br
Accept-Language: zh-CN,zh;q=0.9
Content-Length: 0

";
                client.Write(Encoding.UTF8.GetBytes(content));

                byte[] data = new byte[40960];
                var len = client.InnerStream.Read(data, 0, data.Length);
                var text = Encoding.UTF8.GetString(data, 0, len);
            }
            client.Dispose();
        }

        [TestMethod]
        public void ProxyClientIPV6Test()
        {

            var ip = Dns.GetHostAddresses("localhost").FirstOrDefault(m=>m.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6).ToString();
          
            JMS.ProxyClient client = new JMS.ProxyClient(new JMS.NetAddress("127.0.0.1", 8918), null);
            client.Connect(new JMS.NetAddress(ip, 8911));

            for (int i = 0; i < 2; i++)
            {
                var content = @"GET /?GetAllServiceProviders HTTP/1.1
Host: mail.qq.com
Connection: keep-alive
User-Agent: JmsInvoker
Accept: text/html
Accept-Encoding: deflate, br
Accept-Language: zh-CN,zh;q=0.9
Content-Length: 0

";
                client.Write(Encoding.UTF8.GetBytes(content));

                byte[] data = new byte[40960];
                var len = client.InnerStream.Read(data, 0, data.Length);
                var text = Encoding.UTF8.GetString(data, 0, len);
            }
            client.Dispose();
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
