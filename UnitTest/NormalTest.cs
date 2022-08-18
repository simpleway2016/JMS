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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnitTest.ServiceHosts;

namespace UnitTest
{
    [TestClass]
    public class NormalTest
    {
        int _gateWayPort = 9800;
        int _UserInfoServicePort = 9801;
        bool _userInfoServiceReady = false;
        public void StartGateway()
        {
            Task.Run(() =>
            {
                var builder = new ConfigurationBuilder();
                builder.AddJsonFile("appsettings-gateway.json", optional: true, reloadOnChange: true);
                var configuration = builder.Build();

                JMS.GatewayProgram.Run(configuration, _gateWayPort);
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

                msp.Register<TestUserInfoController>("UserInfoService");
                msp.ServiceProviderBuilded += UserInfo_ServiceProviderBuilded;
                msp.Build(_UserInfoServicePort, gateways)
                    .Run();
            });
        }

        private void UserInfo_ServiceProviderBuilded(object? sender, IServiceProvider e)
        {
            _userInfoServiceReady = true;
            Debug.WriteLine("UserInfoService就绪");
        }

        [TestMethod]
        public void Commit()
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

            using ( var client = new RemoteClient(gateways))
            {
                var serviceClient = client.TryGetMicroService("UserInfoService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("UserInfoService");
                }

                client.BeginTransaction();
                serviceClient.Invoke("SetUserName", "Jack");
                serviceClient.Invoke("SetAge", 28);
                serviceClient.InvokeAsync("SetFather", "Tom");
                serviceClient.InvokeAsync("SetMather", "Lucy");

                Thread.Sleep(2000);
                client.CommitTransaction();
            }

            Debug.WriteLine($"结果：{UserInfoDbContext.FinallyUserName}");

            if (UserInfoDbContext.FinallyUserName != "Jack" ||
                UserInfoDbContext.FinallyAge != 28 ||
                UserInfoDbContext.FinallyFather != "Tom" ||
                UserInfoDbContext.FinallyMather != "Lucy")
                throw new Exception("结果不正确");
        }

        [TestMethod]
        public void Rollback()
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
                serviceClient.Invoke("SetAge", 28);
                serviceClient.InvokeAsync("SetFather", "Tom");
                serviceClient.InvokeAsync("SetMather", "Lucy");

                Thread.Sleep(2000);
                client.RollbackTransaction();

               


            }

            if (UserInfoDbContext.FinallyUserName !=  null||
                UserInfoDbContext.FinallyAge != 0 ||
                UserInfoDbContext.FinallyFather !=null ||
                UserInfoDbContext.FinallyMather != null)
                throw new Exception("结果不正确");
        }

        /// <summary>
        /// 没有事务
        /// </summary>
        /// <exception cref="Exception"></exception>
        [TestMethod]
        public void NoTransaction()
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

            using (var client = new RemoteClient(gateways))
            {
                var serviceClient = client.TryGetMicroService("UserInfoService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("UserInfoService");
                }

                serviceClient.Invoke("SetUserName", "Jack");
                serviceClient.Invoke("SetAge", 28);
                serviceClient.InvokeAsync("SetFather", "Tom");
                serviceClient.InvokeAsync("SetMather", "Lucy");
            }

            Debug.WriteLine($"结果：{UserInfoDbContext.FinallyUserName}");

            if (UserInfoDbContext.FinallyUserName != "Jack" ||
                UserInfoDbContext.FinallyAge != 28 ||
                UserInfoDbContext.FinallyFather != "Tom" ||
                UserInfoDbContext.FinallyMather != "Lucy")
                throw new Exception("结果不正确");
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
    }
}
