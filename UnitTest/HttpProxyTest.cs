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
using JMS;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using JMS.HttpProxyDevice;
using Microsoft.AspNetCore.Http;
using System.Net.Sockets;

namespace UnitTest
{
    [TestClass]
    public class HttpProxyTest
    {
        static bool HasStartWebServer;
        const int WebServerPort = 30001;
        static HttpProxyTest()
        {
            new Thread(async () =>
            {
                await HttpProxyProgram.Main(new[] { "-s:appsettings.httpproxy.json" });
            }).Start();

            while (true)
            {
                Thread.Sleep(500);
                if (HttpProxyProgram.Config == null)
                    continue;
                try
                {
                    foreach (var config in HttpProxyProgram.Config.Current.Servers)
                    {
                        var netclient = new NetClient();
                        netclient.Connect(new NetAddress("127.0.0.1", config.Port));
                        netclient.Dispose();
                    }

                    //全部server已启动
                    break;
                }
                catch
                {

                }
            }


            new Thread(async () =>
            {
                await HttpProxyDeviceProgram.Main(new[] { "-s:appsettings.httpproxydevice.json" });
            }).Start();

        }


        public void StartWebServer( )
        {
            if (HasStartWebServer)
            {
                return;
            }
         
            var builder = WebApplication.CreateBuilder(new string[] { "--urls", "http://*:" + WebServerPort });
          
            var app = builder.Build();

            app.Use((HttpContext context,Func<Task> next) =>
            {
                return context.Response.WriteAsync("abc");
            });
            app.RunAsync();
            HasStartWebServer = true;
        }

        [TestMethod]
        public void HttpTest()
        {
            StartWebServer();

            using var httpClient = new HttpClient();
            for (int i = 0; i < 5; i++)
            {
                var ret = httpClient.GetStringAsync("http://localhost:20001/test/getname").GetAwaiter().GetResult();
                Assert.AreEqual(ret, "abc");
            }

        }

        [TestMethod]
        public void SocketTest()
        {
          
            //启动一个tcp server
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 30002);
            tcpListener.Start();

            new Thread(() =>
            {
                while (true)
                {
                    if (tcpListener == null)
                        return;
                    try
                    {
                        var socket = tcpListener.AcceptSocket();
                        if (socket != null)
                        {
                            using var netClient = new NetClient(socket);
                            byte[] bs = new byte[1024];
                            netClient.ReadData(bs, 0, 3);
                            netClient.Write(bs);
                            try
                            {
                                netClient.ReadBoolean();
                            }
                            catch
                            {

                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }).Start();


            for (int i = 0; i < 5; i++)
            {
                using var netClient = new NetClient();
                netClient.Connect(new NetAddress("127.0.0.1", 20002));
                netClient.Write(new byte[] { 0x1, 0x2, 0x3 });
                byte[] bs = new byte[1024];
                netClient.ReadData(bs, 0, 3);
                if (bs[0] != 0x1 || bs[1] != 0x2 || bs[2] != 0x3)
                    throw new Exception("结果错误");
            }

            tcpListener.Dispose();
            tcpListener = null;
        }

        [TestMethod]
        public void DirectSocketTest()
        {

            //启动一个tcp server
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 30003);
            tcpListener.Start();

            new Thread(() =>
            {
                while (true)
                {
                    if (tcpListener == null)
                        return;
                    try
                    {
                        var socket = tcpListener.AcceptSocket();
                        if (socket != null)
                        {
                            using var netClient = new NetClient(socket);
                            byte[] bs = new byte[1024];
                            netClient.ReadData(bs, 0, 3);
                            netClient.Write(bs);
                            try
                            {
                                netClient.ReadBoolean();
                            }
                            catch
                            {

                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }).Start();


            for (int i = 0; i < 5; i++)
            {
                using var netClient = new NetClient();
                netClient.Connect(new NetAddress("127.0.0.1", 20003));
                netClient.Write(new byte[] { 0x1, 0x2, 0x3 });
                byte[] bs = new byte[1024];
                netClient.ReadData(bs, 0, 3);
                if (bs[0] != 0x1 || bs[1] != 0x2 || bs[2] != 0x3)
                    throw new Exception("结果错误");
            }

            tcpListener.Dispose();
            tcpListener = null;
        }
    }
}
