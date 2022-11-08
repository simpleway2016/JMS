using JMS;
using JMS.Domains;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto.Engines;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        public int _gateWayPort = 9800;
        public int _clusterGateWayPort1 = 10001;
        public int _clusterGateWayPort2 = 10002;
        public int _UserInfoServicePort = 9801;
        public int _CrashServicePort = 9802;
        public int _UserInfoServicePort_forcluster = 9803;
        public bool _userInfoServiceReady = false;

        Gateway _clusterGateway1;
        Gateway _clusterGateway2;
        public void StartGateway()
        {
            Task.Run(() =>
            {
                var builder = new ConfigurationBuilder();
                builder.AddJsonFile("appsettings-gateway.json", optional: true, reloadOnChange: true);
                var configuration = builder.Build();

                JMS.GatewayProgram.Run(configuration, _gateWayPort,out Gateway g);
            });
        }

        public void StartGateway_Cluster1()
        {
            Task.Run(() =>
            {
                var builder = new ConfigurationBuilder();
                builder.AddJsonFile("appsettings-gateway - cluster1.json", optional: true, reloadOnChange: true);
                var configuration = builder.Build();

                JMS.GatewayProgram.Run(configuration, _clusterGateWayPort1, out _clusterGateway1);
            });
        }

        public void StartGateway_Cluster2()
        {
            Task.Run(() =>
            {
                var builder = new ConfigurationBuilder();
                builder.AddJsonFile("appsettings-gateway - cluster2.json", optional: true, reloadOnChange: true);
                var configuration = builder.Build();

                JMS.GatewayProgram.Run(configuration, _clusterGateWayPort2, out _clusterGateway2);
            });
        }

        public void WaitGatewayReady(int port)
        {
            //等待网关就绪
            while (true)
            {
                try
                {
                    var client = new NetClient("127.0.0.1", port);
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

                WaitGatewayReady(_gateWayPort);
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
                msp.ClientCheckCode = @"
            if(headers.TryGetValue(""UserId"",out string userid))
            {
                return true;
            }
            return true;
";
                msp.Register<TestUserInfoController>("UserInfoService");
                msp.ServiceProviderBuilded += UserInfo_ServiceProviderBuilded;
                msp.Build(_UserInfoServicePort, gateways)
                    .Run();
            });
        }


        public void StartUserInfoServiceHost_ForClusterGateways()
        {
            Task.Run(() =>
            {

                WaitGatewayReady(_clusterGateWayPort1);
                WaitGatewayReady(_clusterGateWayPort2);

                ServiceCollection services = new ServiceCollection();

                var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = _clusterGateWayPort1
                   },
                    new NetAddress{
                        Address = "localhost",
                        Port = _clusterGateWayPort2
                   }
                };

                services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddConsole(); // 将日志输出到控制台
                });

                services.AddScoped<UserInfoDbContext>();
                var msp = new MicroServiceHost(services);
                msp.RetryCommitPath = "./$$JMS_RetryCommitPath_Cluster_" + _UserInfoServicePort_forcluster;
                msp.Register<TestUserInfoController>("UserInfoService");
                msp.ServiceProviderBuilded += UserInfo_ServiceProviderBuilded;
                msp.Build(_UserInfoServicePort_forcluster, gateways)
                    .Run();
            });
        }

        public void StartCrashServiceHost(int port)
        {
            Task.Run(() =>
            {

                WaitGatewayReady(_gateWayPort);
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
        public void Commit()
        {
            UserInfoDbContext.Reset();
            StartGateway();
            StartUserInfoServiceHost();

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

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
                serviceClient.Invoke("CheckTranId");
                serviceClient.Invoke("SetUserName", "Jack");
                serviceClient.Invoke("SetAge", 28);
                serviceClient.InvokeAsync("SetFather", "Tom");
                serviceClient.InvokeAsync("SetMather", "Lucy");

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
            UserInfoDbContext.Reset();
            StartGateway();
            StartUserInfoServiceHost();

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

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
                client.RollbackTransaction();

            }

            if (UserInfoDbContext.FinallyUserName !=  null||
                UserInfoDbContext.FinallyAge != 0 ||
                UserInfoDbContext.FinallyFather !=null ||
                UserInfoDbContext.FinallyMather != null)
                throw new Exception("结果不正确");
        }

        [TestMethod]
        public void RollbackForError()
        {
            UserInfoDbContext.Reset();
            StartGateway();
            StartUserInfoServiceHost();

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = _gateWayPort
                   }
                };

            try
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
                    serviceClient.Invoke("SetAge", 28);

                    serviceClient.InvokeAsync("SetFather", "Tom");
                    serviceClient.InvokeAsync("SetMather", "Lucy");
                    serviceClient.InvokeAsync("BeError");//这个方法调用会有异常
                    client.CommitTransaction();

                }
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException.Message;
                if (msg != "有意触发错误")
                    throw ex;
            }

            if (UserInfoDbContext.FinallyUserName != null ||
                UserInfoDbContext.FinallyAge != 0 ||
                UserInfoDbContext.FinallyFather != null ||
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
            UserInfoDbContext.Reset();
            StartGateway();
            StartUserInfoServiceHost();

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

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

        /// <summary>
        /// 测试提交时，有个服务宕机
        /// </summary>
        /// <exception cref="Exception"></exception>
        [TestMethod]
        public void TestCrash()
        {
            UserInfoDbContext.Reset();
            StartGateway();
            StartUserInfoServiceHost();
            StartCrashServiceHost(_CrashServicePort);

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

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

                var crashService = client.TryGetMicroService("CrashService");
                while (crashService == null)
                {
                    Thread.Sleep(10);
                    crashService = client.TryGetMicroService("CrashService");
                }

                client.BeginTransaction();

                serviceClient.Invoke("SetUserName", "Jack");
                crashService.Invoke("SetText", "abc");
                try
                {
                    client.CommitTransaction();
                }
                catch (Exception ex)
                {

                    Debug.WriteLine(ex.Message);
                }
               
            }

            Thread.Sleep(7000);//等待7秒，失败的事务

            if (UserInfoDbContext.FinallyUserName != "Jack" || TestCrashController.FinallyText != "abc")
                throw new Exception("结果不正确");
        }

        [TestMethod]
        public void TestCrashForLocal()
        {
            TestCrashController.CanCrash = true;
            try
            {
                Directory.Delete("./$$_JMS.Invoker.Transactions", true);
            }
            catch (Exception)
            {

            }
            UserInfoDbContext.Reset();
            StartGateway();
            StartUserInfoServiceHost();
            StartCrashServiceHost(_CrashServicePort);

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = _gateWayPort
                   }
                };

            string tranid;
            using (var client = new RemoteClient(gateways))
            {
                var serviceClient = client.TryGetMicroService("UserInfoService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("UserInfoService");
                }

                var crashService = client.GetMicroService("CrashService",new JMS.Dtos.RegisterServiceLocation { 
                    ServiceAddress = "127.0.0.1",
                    Port = _CrashServicePort
                } );               

                client.BeginTransaction();
                tranid = client.TransactionId;

                serviceClient.Invoke("SetUserName", "Jack");
                crashService.Invoke("SetText", "abc");
                try
                {
                    client.CommitTransaction();
                }
                catch (Exception ex)
                {

                    Debug.WriteLine(ex.Message);
                }

            }

            while(File.Exists($"./$$_JMS.Invoker.Transactions/{tranid}.json"))
            {
                Thread.Sleep(1000);
            }

            if (UserInfoDbContext.FinallyUserName != "Jack" || TestCrashController.FinallyText != "abc")
                throw new Exception("结果不正确");
        }

        /// <summary>
        /// 测试网关集群
        /// </summary>
        [TestMethod]
        public void TestGatewayCluster()
        {
            UserInfoDbContext.Reset();
            StartGateway_Cluster1();
            StartGateway_Cluster2();

            //等待网关就绪
            WaitGatewayReady(_clusterGateWayPort1);
            WaitGatewayReady(_clusterGateWayPort2);

            StartUserInfoServiceHost_ForClusterGateways();

            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = _clusterGateWayPort1
                   },new NetAddress{
                        Address = "localhost",
                        Port = _clusterGateWayPort2
                   }
                };

            var serviceProvider1 =(IServiceProvider) _clusterGateway1.GetType().GetProperty("ServiceProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_clusterGateway1);
            var clusterGatewayConnector1 = serviceProvider1.GetService<ClusterGatewayConnector>();

            var serviceProvider2 = (IServiceProvider)_clusterGateway2.GetType().GetProperty("ServiceProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_clusterGateway2);
            var clusterGatewayConnector2 = serviceProvider2.GetService<ClusterGatewayConnector>();

            Debug.WriteLine("等待决出主网关");
            while (clusterGatewayConnector1.IsMaster == false && clusterGatewayConnector2.IsMaster == false)
            {
                Thread.Sleep(100);
            }

            var masterGateway = clusterGatewayConnector1.IsMaster ? _clusterGateway1 : _clusterGateway2;
            var lockManager = (clusterGatewayConnector1.IsMaster ? serviceProvider1 : serviceProvider2).GetService<LockKeyManager>();

            using (var client = new RemoteClient(gateways))
            {
                Debug.WriteLine("查找服务");
                var serviceClient = client.TryGetMicroService("UserInfoService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("UserInfoService");
                }
                Debug.WriteLine("查找服务完毕");

                serviceClient.Invoke("LockName",  "abc" , "d","e","f" );

                if(lockManager.GetAllKeys().Any(k=>k.Key == "abc") == false)
                {
                    throw new Exception("找不到lock key");
                }
                serviceClient.Invoke("UnlockName", "d");
            }

            var slaveGateway = clusterGatewayConnector1.IsMaster ? _clusterGateway2 : _clusterGateway1;
            var slaveLockManager = (clusterGatewayConnector1.IsMaster ? serviceProvider2 : serviceProvider1).GetService<LockKeyManager>();
            while(slaveLockManager.GetAllKeys().Any(k => k.Key == "f") == false)
            {
                Thread.Sleep(1000);
            }

            //关闭主网关
            masterGateway.Dispose();

            var serviceProvider = (IServiceProvider)slaveGateway.GetType().GetProperty("ServiceProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(slaveGateway);
            var clusterGatewayConnector = serviceProvider.GetService<ClusterGatewayConnector>();

            //等待从网关成为主网关
            while(clusterGatewayConnector.IsMaster == false)
            {
                Thread.Sleep(100);
            }

            while(slaveLockManager.GetAllKeys().Any(m=>m.RemoveTime != null))
            {
                Thread.Sleep(100);
            }
            if (slaveLockManager.GetAllKeys().Length != 3)
            {
                throw new Exception("lock key数量不对");
            }
        }
    }
}
