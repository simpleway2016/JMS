using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Threading;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using UnitTest.Controllers;
using System.Reflection;
using System.Net.Http;
using JMS;
using static System.Net.Mime.MediaTypeNames;
using UnitTest.ServiceHosts;

namespace UnitTest
{
    [TestClass]
    public class AspNetCoreTest
    {
        WebApplication StartWebApi(int gateWayPort)
        {
            var builder = WebApplication.CreateBuilder(new string[] { "--urls", "http://*:9000" });
            builder.Services.AddControllers();
            var gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", gateWayPort) };
            

            builder.Services.AddMvcCore()
.ConfigureApplicationPartManager(manager =>
{
    var featureProvider = new MyFeatureProvider();
    manager.FeatureProviders.Add(featureProvider);
});
            builder.Services.RegisterJmsService("http://127.0.0.1:9000", "TestWebService", gateways , option => {
                option.RetryCommitPath += "9000";
            });
            var app = builder.Build();

            app.UseAuthentication();    //认证
            app.UseAuthorization();     //授权

            app.UseJmsService();

            app.MapControllers();
            return app;
        }

        WebApplication StartCrashWebApi(int gateWayPort)
        {
            var builder = WebApplication.CreateBuilder(new string[] { "--urls", "http://*:9001" });
            builder.Services.AddControllers();
            var gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", gateWayPort) };


            builder.Services.AddMvcCore()
.ConfigureApplicationPartManager(manager =>
{
    var featureProvider = new MyCrashFeatureProvider();
    manager.FeatureProviders.Add(featureProvider);
});
            builder.Services.RegisterJmsService("http://127.0.0.1:9001", "TestCrashService", gateways, option => {
                option.RetryCommitPath += "9001";
            });
            var app = builder.Build();

            app.UseAuthentication();    //认证
            app.UseAuthorization();     //授权

            app.UseJmsService();

            app.MapControllers();
            return app;
        }

        [TestMethod]
        public void HttpPostFormTest()
        {
            var normalTest = new NormalTest();
            normalTest.StartGateway();

            var app = StartWebApi(normalTest._gateWayPort);
            app.RunAsync();
            Thread.Sleep(1000);
            normalTest.WaitGatewayReady(normalTest._gateWayPort);


            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = normalTest._gateWayPort
                   }
                };

            using (var remoteClient = new RemoteClient(gateways))
            {

                while (true)
                {
                    if (remoteClient.TryGetMicroService("TestWebService") == null)
                        Thread.Sleep(100);
                    else
                        break;
                }
            }

            HttpClient client = new HttpClient();
            List<KeyValuePair<string, string>> param = new List<KeyValuePair<string, string>>();
            param.Add(new KeyValuePair<string, string>("name", "jack"));
            param.Add(new KeyValuePair<string, string>("age", "1"));

            //通过网关反向代理访问webapi
            var ret = client.PostAsync($"http://localhost:{normalTest._gateWayPort}/TestWebService/WeatherForecast" ,new FormUrlEncodedContent(param)).ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == false)
                throw new Exception("http访问失败");
            var text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (text != "jack1")
                throw new Exception("http返回结果错误");
            //app.StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

      

        /// <summary>
        /// 测试线程池是否多次创建
        /// </summary>
        /// <exception cref="Exception"></exception>
        [TestMethod]
        public void HttpTestForClientPool()
        {
            var normalTest = new NormalTest();
            normalTest.StartGateway();

            var app = StartWebApi(normalTest._gateWayPort);
            app.RunAsync();
            Thread.Sleep(1000);
            normalTest.WaitGatewayReady(normalTest._gateWayPort);


            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = normalTest._gateWayPort
                   }
                };

            using (var remoteClient = new RemoteClient(gateways))
            {

                while (true)
                {
                    if (remoteClient.TryGetMicroService("TestWebService") == null)
                        Thread.Sleep(100);
                    else
                        break;
                }
            }

            int createCount = 0;
            NetClientPool.CreatedNewClient += (s, newClient) => {
                Interlocked.Increment(ref createCount);
            };
            for (int i = 0; i < 10; i++)
            {
                HttpClient client = new HttpClient();
                //通过网关反向代理访问webapi
                var ret = client.GetAsync($"http://localhost:{normalTest._gateWayPort}/TestWebService/WeatherForecast").ConfigureAwait(false).GetAwaiter().GetResult();
                if (ret.IsSuccessStatusCode == false)
                    throw new Exception("http访问失败");
                var text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                if (text.StartsWith("[{\"date\":") == false)
                    throw new Exception("http返回结果错误");
            }
            if (createCount > 1)
                throw new Exception("多次创建client连接");
        }

        [TestMethod]
        public void AsyncHttpTest()
        {
            var normalTest = new NormalTest();
            normalTest.StartGateway();

            var app = StartCrashWebApi(normalTest._gateWayPort);
            app.RunAsync();
            Thread.Sleep(1000);
            normalTest.WaitGatewayReady(normalTest._gateWayPort);


            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = normalTest._gateWayPort
                   }
                };

            using (var remoteClient = new RemoteClient(gateways))
            {

                var service2 = remoteClient.TryGetMicroService("TestCrashService");
                while (service2 == null)
                {
                    Thread.Sleep(100);
                    service2 = remoteClient.TryGetMicroService("TestCrashService");
                }
                try
                {
                    service2.Invoke("/Crash/AsyncSetName");
                    throw new Exception("error");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("改为"))
                        return;
                    throw ex;
                }

            }

          
        }

        [TestMethod]
        public void MicroServiceTest()
        {
            var normalTest = new NormalTest();
            normalTest.StartGateway();

            var app = StartWebApi(normalTest._gateWayPort);
            app.RunAsync();
            Thread.Sleep(1000);
            normalTest.WaitGatewayReady(normalTest._gateWayPort);


            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = normalTest._gateWayPort
                   }
                };

            try
            {

                using (var remoteClient = new RemoteClient(gateways))
                {
                    var service = remoteClient.TryGetMicroService("TestWebService");
                    while (service == null)
                    {
                        Thread.Sleep(100);
                        service = remoteClient.TryGetMicroService("TestWebService");
                    }

                    var ret = service.Invoke<WeatherForecast[]>("/WeatherForecast/Get");
                    if (ret.Length != 5)
                        throw new Exception("返回结果错误");
                }
            }
            finally
            {
               
            }
        }

        [TestMethod]
        public void CrashTest()
        {
            var normalTest = new NormalTest();
            normalTest.StartGateway();

            var app = StartWebApi(normalTest._gateWayPort);
            app.RunAsync();

            var crash_app = StartCrashWebApi(normalTest._gateWayPort);
            crash_app.RunAsync();

            Thread.Sleep(1000);


            try
            {
                normalTest.WaitGatewayReady(normalTest._gateWayPort);


                var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = normalTest._gateWayPort
                   }
                };

                using (var remoteClient = new RemoteClient(gateways))
                {
                    var service = remoteClient.TryGetMicroService("TestWebService");
                    while (service == null)
                    {
                        Thread.Sleep(100);
                        service = remoteClient.TryGetMicroService("TestWebService");
                    }

                    var service2 = remoteClient.TryGetMicroService("TestCrashService");
                    while (service2 == null)
                    {
                        Thread.Sleep(100);
                        service2 = remoteClient.TryGetMicroService("TestCrashService");
                    }

                    remoteClient.BeginTransaction();

                    var ret = service.Invoke<WeatherForecast[]>("/WeatherForecast/Get");
                    if (ret.Length != 5)
                        throw new Exception("返回结果错误");

                    var name = service2.Invoke<string>("/Crash/SetName" , "Jack");

                    try
                    {
                        remoteClient.CommitTransaction();
                    }
                    catch (Exception ex)
                    {

                        Debug.WriteLine(ex.Message);
                    }
                }

                Thread.Sleep(7000);//等待7秒，失败的事务

                if (CrashController.FinallyUserName != "Jack")
                    throw new Exception("结果不正确");
            }
            finally
            {
               
            }
        }

       
    }

    class MyFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Add(typeof(WeatherForecastController).GetTypeInfo());
        }
    }
    class MyCrashFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Add(typeof(CrashController).GetTypeInfo());
        }
    }
}
