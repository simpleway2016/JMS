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
using JMS.ServerCore;
using System.IO;
using System.Net.Sockets;
using JMS.Common;

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
            builder.Services.RegisterJmsService("http://127.0.0.1:9000", "TestWebService", gateways ,true, option => {
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
            builder.Services.RegisterJmsService("http://127.0.0.1:9001", "TestCrashService", gateways, true, option => {
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
        public void PipelineHeaderTest()
        {
            var headers = new Dictionary<string, string>();
            string reqline = null;
            var actionAccept = new Action<Socket>(async socket => {
                var serverClient = new NetClient(socket);
              
                reqline = await HttpHelper.ReadHeaders(serverClient.PipeReader, headers);
            });
            Task.Run(() => {
                var tcplistener = new TcpListener(10001);
                tcplistener.Start();

                while (true)
                {
                    var socket = tcplistener.AcceptSocket();
                    actionAccept(socket);
                }
            });

            Thread.Sleep(500);
            var client = new NetClient();
            client.Connect(new NetAddress("127.0.0.1",10001));
            var data = Encoding.UTF8.GetBytes("GET /test");
            client.InnerStream.Write(data , 0 , data.Length);
            Thread.Sleep(2000);
           
            for(int i = 0;i < 6000; i ++)
            {
                client.InnerStream.Write(new byte[1] { (byte)'a'}, 0, 1);

            }
            data = Encoding.UTF8.GetBytes(@"1awefijawofjewaofjawojfeowajfoawjoefoawjfijoaw9
Host: abc.com
");
            client.InnerStream.Write(data, 0, data.Length);

            data = Encoding.UTF8.GetBytes(@"Ca");
            client.InnerStream.Write(data, 0, data.Length);

            Thread.Sleep(2000);
            data = Encoding.UTF8.GetBytes(@": true

");
            client.InnerStream.Write(data, 0, data.Length);

            while (reqline == null)
                Thread.Sleep(500);

            if(reqline.Length > 6000 && reqline.EndsWith("aw9"))
            {

            }
            else
            {
                throw new Exception("结果不对");
            }

            if (headers["Ca"] != "true")
                throw new Exception("结果不对");
        }

        [TestMethod]
        public void PipelineTest()
        {
            var headers = new Dictionary<string, string>();
            string reqline1 = null;
            string reqline2 = null;
            string reqline3 = null;
            var actionAccept = new Action<Socket>(async socket => {
                var serverClient = new NetClient(socket);

                reqline1 = await serverClient.ReadLineAsync();
                reqline2 = await serverClient.ReadLineAsync();
                reqline3 = await serverClient.ReadLineAsync();
            });
            Task.Run(() => {
                var tcplistener = new TcpListener(10002);
                tcplistener.Start();

                while (true)
                {
                    var socket = tcplistener.AcceptSocket();
                    actionAccept(socket);
                }
            });

            Thread.Sleep(500);
            var client = new NetClient();
            client.Connect(new NetAddress("127.0.0.1", 10002));
            var data = Encoding.UTF8.GetBytes("GET /test");
            client.InnerStream.Write(data, 0, data.Length);
            Thread.Sleep(2000);

            for (int i = 0; i < 6000; i++)
            {
                client.InnerStream.Write(new byte[1] { (byte)'a' }, 0, 1);

            }
            data = Encoding.UTF8.GetBytes(@"1awefijawofjewaofjawojfeowajfoawjoefoawjfijoaw9
Host: abc.com
");
            client.InnerStream.Write(data, 0, data.Length);

            data = Encoding.UTF8.GetBytes(@"Ca");
            client.InnerStream.Write(data, 0, data.Length);

            Thread.Sleep(2000);
            data = Encoding.UTF8.GetBytes(@": true

");
            client.InnerStream.Write(data, 0, data.Length);

            while (reqline3 == null)
                Thread.Sleep(500);

            if (reqline1.Length > 6000 && reqline1.EndsWith("aw9"))
            {

            }
            else
            {
                throw new Exception("结果不对");
            }
            if (reqline2 != "Host: abc.com")
                throw new Exception("结果不对");
            if (reqline3 != "Ca: true")
                throw new Exception("结果不对");

        }

        [TestMethod]
        public void TestHttpHeader()
        {
            var headers = new Dictionary<string, string>();
            var content = @"GET / HTTP/1.1
Host: mail.qq.com
Connection: keep-alive
User-Agent: JmsInvoker
Accept: text/html
Accept-Encoding: deflate, br
Accept-Language: zh-CN,zh;q=0.9
Content-Length: 0
Host2: mail.qq.com
Connection2: keep-alive
User-Agent2: JmsInvoker
Accept2: text/html
Accept-Encoding2: deflate, br
Accept-Language2: zh-CN,zh;q=0.9
Content-Length2: 0

";
            StringBuilder strRet = new StringBuilder();
            long? time = null;
            Task.Run(async () => {
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                var reader = System.IO.Pipelines.PipeReader.Create(stream);
                var line = await HttpHelper.ReadHeaders(reader, headers);

                stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                reader = System.IO.Pipelines.PipeReader.Create(stream);
                Stopwatch sw = new Stopwatch();
                sw.Start();
                line = await HttpHelper.ReadHeaders(reader, headers);
                sw.Stop();
                time = sw.ElapsedMilliseconds;
                strRet.AppendLine(line);
            });
            while(time == null)
            Thread.Sleep(100);

            if (time > 2)
                throw new Exception("解析时间太长");

           
            foreach( var pair in headers)
            {
                strRet.AppendLine($"{pair.Key}: {pair.Value}");
            }
            strRet.AppendLine("");
            if(strRet.ToString() != content)
            {
                throw new Exception("解析后内容不正确");
            }
        }

        [TestMethod]
        public void HttpPostFormTest()
        {
            var normalTest = new NormalTest();
            normalTest.StartGateway();
            normalTest.StartJmsWebApi();
            normalTest.StartWebApi(normalTest._gateWayPort).RunAsync();

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

            using (var remoteClient = new RemoteClient("localhost" , normalTest._gateWayPort))
            {
                remoteClient.ListMicroService(null);
                remoteClient.ListMicroServiceAsync(null).GetAwaiter().GetResult();
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


            //通过webapi反向代理访问webapi
            ret = client.PostAsync($"http://localhost:{normalTest._webApiPort}/JMSRedirect/TestWebService/WeatherForecast", new FormUrlEncodedContent(param)).ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == false)
                throw new Exception("http访问失败");
            text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (text != "jack1")
                throw new Exception("http返回结果错误");

            //通过jmswebapi反向代理访问webapi
            ret = client.PostAsync($"http://localhost:{normalTest._jmsWebapiPort}/TestWebService/WeatherForecast", new FormUrlEncodedContent(param)).ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == false)
                throw new Exception("http访问失败");
            text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
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
            normalTest.StartJmsWebApi();

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
            for (int i = 0; i < 10; i++)
            {
                HttpClient client = new HttpClient();
                //通过网关反向代理访问webapi
                var ret = client.GetAsync($"http://localhost:{normalTest._jmsWebapiPort}/TestWebService/WeatherForecast").ConfigureAwait(false).GetAwaiter().GetResult();
                if (ret.IsSuccessStatusCode == false)
                    throw new Exception("http访问失败");
                var text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                if (text.StartsWith("[{\"date\":") == false)
                    throw new Exception("http返回结果错误");
            }
            for (int i = 0; i < 10; i++)
            {
                HttpClient client = new HttpClient();
                //通过网关反向代理访问webapi
                var ret = client.GetAsync($"http://localhost:{normalTest._jmsWebapiPort}/TestWebService/WeatherForecast").ConfigureAwait(false).GetAwaiter().GetResult();
                if (ret.IsSuccessStatusCode == false)
                    throw new Exception("http访问失败");
                var text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                if (text.StartsWith("[{\"date\":") == false)
                    throw new Exception("http返回结果错误");
            }
            if (createCount > 2)
                throw new Exception("多次创建client连接");
        }

        void startSocket5Proxy()
        {
            Task.Run(() => {
                JMS.Proxy.Program.Main(new string[] { "8918" });
            });
        }

        [TestMethod]
        public void AsyncHttpTest()
        {
            startSocket5Proxy();
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
                service2 = remoteClient.TryGetMicroServiceAsync("TestCrashService").GetAwaiter().GetResult();
                try
                {
                    service2.Invoke("/Crash/AsyncSetName");
                    throw new Exception("error");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("改为"))
                    { }
                    else
                    {
                        throw ex;
                    }
                }

            }

            using (var remoteClient = new RemoteClient(gateways, new NetAddress("127.0.0.1", 8918)))
            {

                var service2 = remoteClient.TryGetMicroService("TestCrashService");

                var name = service2.Invoke<string>("/Crash/GetName");
                Debug.Assert(name == "Jack", "结果错误");
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

                    var ret = service.Invoke<WeatherForecast[]>("/MyWeatherForecast/Get");
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

                    var ret = service.Invoke<WeatherForecast[]>("/MyWeatherForecast/Get");
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

                Thread.Sleep(12000);//等待7秒，失败的事务

                if (CrashController.FinallyUserName != "Jack")
                    throw new Exception("结果不正确");
            }
            finally
            {
               
            }
        }

        [TestMethod]
        public void ScropeTest()
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


                    service2.Invoke("/Crash/AsyncSetName", "Jack2");
                    service2.Invoke("/Crash/AsyncSetName", "Jack");
                    service2.InvokeAsync("/Crash/AsyncSetName", "Jack1");
                    service2.InvokeAsync("/Crash/AsyncSetName", "Jack2");
                    service2.InvokeAsync("/Crash/AsyncSetName", "Jack");

                    remoteClient.CommitTransaction();
                }

                if (CrashController.FinallyUserName != "Jack2")  //因为最后一个事务会先提交，然后再提交之前的事务，那么最后提交的事务应该是Jack2那次的调用
                    throw new Exception("结果不正确");

                CrashController.FinallyUserName = null;
                bool createdNewClient = false;
                using (var remoteClient = new RemoteClient(gateways))
                {
                    var service2 = remoteClient.TryGetMicroService("TestCrashService");

                    remoteClient.BeginTransaction();

                    NetClientPool.CreatedNewClient += (s, e) => {
                        createdNewClient = true;
                    };

                    service2.Invoke("/Crash/AsyncSetName", "Jack2");
                    service2.Invoke("/Crash/AsyncSetName", "Jack");
                    service2.InvokeAsync("/Crash/AsyncSetName", "Jack1");
                    service2.InvokeAsync("/Crash/AsyncSetName", "Jack2");
                    service2.InvokeAsync("/Crash/AsyncSetName", "Jack");

                    remoteClient.CommitTransaction();
                }

                if (createdNewClient)
                    throw new Exception("创建了新的连接");
                if (CrashController.FinallyUserName != "Jack2")  //因为最后一个事务会先提交，然后再提交之前的事务，那么最后提交的事务应该是Jack2那次的调用
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
            feature.Controllers.Add(typeof(MyWeatherForecastController).GetTypeInfo());
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
