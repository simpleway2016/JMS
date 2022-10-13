﻿using JMS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Engines;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnitTest.ServiceHosts;
using Way.Lib;

namespace UnitTest
{
    [TestClass]
    public class PerformanceTest
    {
        int _gateWayPort = 9800;
        int _UserInfoServicePort = 9801;
        int _CrashServicePort = 9802;
        bool _userInfoServiceReady = false;
        public void StartGateway()
        {
            Task.Run(() =>
            {
                var builder = new ConfigurationBuilder();
                builder.AddJsonFile("appsettings-gateway.json", optional: true, reloadOnChange: true);
                var configuration = builder.Build();

                JMS.GatewayProgram.Run(configuration, _gateWayPort, out Gateway g);
            });
        }

        void WaitGatewayReady()
        {
            //等待网关就绪
            while (true)
            {
                try
                {
                    var client = new NetClient("127.0.0.1", _gateWayPort);
                    client.Dispose();

                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
        }

        public void StartUserInfoServiceHost()
        {
            Task.Run(() =>
            {

                WaitGatewayReady();
                ServiceCollection services = new ServiceCollection();

                var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = _gateWayPort
                   }
                };

                services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddConsole(); // 将日志输出到控制台
                });

                services.AddScoped<UserInfoDbContext>();
                var msp = new MicroServiceHost(services);
                msp.RetryCommitPath = "./$$JMS_RetryCommitPath" + _UserInfoServicePort;
                msp.Register<TestUserInfoController>("UserInfoService");
                msp.ServiceProviderBuilded += UserInfo_ServiceProviderBuilded;
                msp.Build(_UserInfoServicePort, gateways)
                    .Run();
            });
        }

        public void StartCrashServiceHost(int port)
        {
            Task.Run(() =>
            {

                WaitGatewayReady();
                ServiceCollection services = new ServiceCollection();

                var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = _gateWayPort
                   }
                };

                services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddConsole(); // 将日志输出到控制台
                });

                services.AddScoped<UserInfoDbContext>();
                var msp = new MicroServiceHost(services);
                msp.RetryCommitPath = "./$$JMS_RetryCommitPath" + port;
                msp.Register<TestCrashController>("CrashService");
                msp.ServiceProviderBuilded += UserInfo_ServiceProviderBuilded;
                msp.Build(port, gateways)
                    .Run();
            });
        }

        private void UserInfo_ServiceProviderBuilded(object? sender, IServiceProvider e)
        {
            _userInfoServiceReady = true;
            Debug.WriteLine("UserInfoService就绪");
        }

        [TestMethod]
        public void Performance()
        {
            StartGateway();
            StartUserInfoServiceHost();

            //等待网关就绪
            WaitGatewayReady();

            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = _gateWayPort
                   }
                };

            while (true)
            {
                using (var client = new RemoteClient(gateways))
                {
                    var serviceClient = client.TryGetMicroService("UserInfoService");
                    while (serviceClient == null)
                    {
                        Thread.Sleep(10);
                        serviceClient = client.TryGetMicroService("UserInfoService");
                    }

                    client.BeginTransaction();
                    serviceClient.Invoke("SetUserName", "Jack");
                    serviceClient.Invoke("SetUserName", "Jack2");
                    serviceClient.InvokeAsync("SetUserName", "Jack3");
                    serviceClient.InvokeAsync("SetUserName", "Jack4");
                    client.CommitTransaction();

                    Debug.WriteLine($"结果：{UserInfoDbContext.FinallyUserName}");

                    if (UserInfoDbContext.FinallyUserName.StartsWith("Jack") == false)
                        throw new Exception("结果不正确");

                    UserInfoDbContext.FinallyUserName = null;
                }
            }
        }

        [TestMethod]
        public void UrlParse()
        {
            var httpRequest = "/userinfo/api/users/getName";
            var servieName = httpRequest.Substring(1);
            servieName = servieName.Substring(0, servieName.IndexOf("/"));

            httpRequest = httpRequest.Substring(servieName.Length + 1);

            if (servieName != "userinfo" || httpRequest != "/api/users/getName")
                throw new Exception("结果错误");
        }

        [TestMethod]
        public void CertToJson()
        {
            X509Certificate2 cert = new X509Certificate2("./test.pfx", "123456", X509KeyStorageFlags.Exportable);
            var rawData = cert.RawData;
            var json = new { data = rawData }.ToJsonString();
            var obj = json.FromJson<CertItem>();
            for(int i = 0; i < obj.data.Length; i++)
            {
                if (obj.data[i] != rawData[i])
                    throw new Exception("error");
            }
        }

        [TestMethod]
        public void SocketTest()
        {
            NetStream client = new NetStream("127.0.0.1", 5255);
            client.Socket.Send(new byte[0]);

        }

        int errcount = 0;
        [TestMethod]
        public void ClientPoolTest()
        {
            int newSocketCount = 0;
            TcpListener tcpListener = new TcpListener( IPAddress.Any, 9000);
            tcpListener.Start();
            int connectCount = 0;
            ConcurrentDictionary<NetClient, bool> cache = new ConcurrentDictionary<NetClient, bool>();

            Task.Run(() => {
                Thread.Sleep(1000);
                List<NetClient> clients = new List<NetClient>();
                for(int i = 0; i < 1000; i++)
                {
                    var client = NetClientPool.CreateClient(null, new NetAddress("localhost", 9000), null);
                    clients.Add(client);
                }
                foreach( var client in clients)
                {
                    NetClientPool.AddClientToPool(client);
                }
                var addr = new NetAddress("localhost", 9000);
                Parallel.For(0, 10, i => {
                    while (true)
                    {
                        var client = NetClientPool.CreateClient(null, addr, null);
                        if (cache.ContainsKey(client))
                            throw new Exception("error");
                       if( cache.TryAdd(client, true) == false)
                            throw new Exception("error");
                        client.Socket.ReceiveTimeout = 2000;
                        Interlocked.Increment(ref connectCount);

                        client.Write(new byte[3] { 0x1, 0x2, 0x3 });
                        int c = client.Read(new byte[3]);
                        if (c == 0)
                            throw new Exception("data err");

                        cache.TryRemove(client, out bool o);
                        NetClientPool.AddClientToPool(client);

                        //if(newSocketCount != 1000)
                        //{
                        //    throw new Exception("socket 数量不对");
                        //}
                        Debug.WriteLine($"当前连接数：{connectCount} socket数量{newSocketCount} 错误数量{errcount} {NetClientPool.GetPoolAliveCount(addr)}");
                        Thread.Sleep(0);
                        
                    }
                });
            });

           

            while (true)
            {
                var socket = tcpListener.AcceptSocket();
                socket.ReceiveTimeout = -1;
                Interlocked.Increment(ref newSocketCount);
                handlesocket(socket);
            }
        }

        void handlesocket(Socket socket)
        {
            Task.Run(() => {
                listenerSocketData(socket);
            });
        }

        void listenerSocketData(Socket socket)
        {
            byte[] data = new byte[3];
            while (true)
            {
                var count = socket.Receive(data);
                if (count <= 0)
                {
                    Interlocked.Increment(ref errcount);
                    socket.Close();
                    socket.Dispose();
                    return;

                }
                if (count != 3 || data[0] != 0x1 || data[1] != 0x2 || data[2] != 0x3)
                {
                    throw new Exception("data error");
                }
                socket.Send(data);
            }
        }

        class CertItem
        {
            public byte[] data;
        }
    }
}
