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

namespace UnitTest
{
    [TestClass]
    public class ProxyTest
    {
        [TestMethod]
        public void Socks5Test()
        {
            Task.Run(() => {
                JMS.Proxy.Program.Main(new string[] { "8918"});
            });

            Thread.Sleep(1000);
            var socksProxy = new Socks5ProxyClient("127.0.0.1", 8918);
            var handler = new ProxyHandler(socksProxy);

            HttpClient client = new HttpClient(handler);
            var ret = client.GetStringAsync("https://mail.qq.com/").ConfigureAwait(false).GetAwaiter().GetResult();
            client.Dispose();
        }
    }
}
