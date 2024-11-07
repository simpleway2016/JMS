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
using JMS.Common.Net;
using Extreme.Net.Core.Proxy;
using JMS.IdentityModel.JWT.Authentication;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using JMS.Token;
using Way.Lib;
using System.Collections.Concurrent;
using System.Net.Sockets;
using JMS;
using System.Buffers;
using System.Net.Http.Headers;
using HttpClient = System.Net.Http.HttpClient;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2;
using System.Diagnostics;

namespace UnitTest
{
    [TestClass]
    public class NetClientPoolTest
    {
        [TestMethod]
        public void test()
        {
            int newInstanceCount = 0;

            while (true)
            {

                var client = NetClientPool.CreateClient(null, new JMS.NetAddress("mail.qq.com", 443, true)
                {
                    CertDomain = "mail.qq.com"
                }, (a) =>
                {
                    newInstanceCount++;
                });

                if (newInstanceCount > 1)
                {
                    throw new Exception("异常了");
                }

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

                Dictionary<string, string> headers = new Dictionary<string, string>();
                client.PipeReader.ReadHeaders(headers).GetAwaiter().GetResult();

                int contentLength = Convert.ToInt32(headers["Content-Length"]);
                byte[] data = new byte[contentLength];
                client.ReadData(data, 0, data.Length);
                var text = Encoding.UTF8.GetString(data);

                NetClientPool.AddClientToPool(client);
                Thread.Sleep(1000);
            }
        }


        [TestMethod]
        public void testAsync()
        {
            var info = new VisitInfo();
            asyncRun(info);
            while (true)
            {
                Thread.Sleep(2000);
                Debug.WriteLine($"实例：{info.newInstanceCount} 访问：{info.visitCount}");
            }
        }

        async void asyncRun(VisitInfo info)
        {
            

            await Parallel.ForAsync(1, 3, async (index, can) =>
            {
                while (true)
                {

                    var client = await NetClientPool.CreateClientAsync(null, new JMS.NetAddress("mail.qq.com", 443, true)
                    {
                        CertDomain = "mail.qq.com"
                    }, (a) =>
                    {
                        Interlocked.Increment(ref info.newInstanceCount);
                        return Task.CompletedTask;
                    });
                     
                   

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

                    Dictionary<string, string> headers = new Dictionary<string, string>();
                    await client.PipeReader.ReadHeaders(headers);

                    int contentLength = Convert.ToInt32(headers["Content-Length"]);
                    byte[] data = new byte[contentLength];
                    await client.ReadDataAsync(data, 0, data.Length);
                    var text = Encoding.UTF8.GetString(data);
                    Interlocked.Increment(ref info.visitCount);
                    NetClientPool.AddClientToPool(client);
                    Thread.Sleep(1000);
                }
            });

           
        }

    }

    class VisitInfo
    {
        public int newInstanceCount;
        public int visitCount;
    }

}
