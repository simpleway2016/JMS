using JMS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto.Engines;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        class CertItem
        {
            public byte[] data;
        }
    }
}
