using Extreme.Net.Core.Proxy;
using JMS;
using JMS.Applications;
using JMS.Cluster;
using JMS.Common.Collections;
using JMS.Dtos;
using JMS.ServerCore;
using JMS.WebApiDocument;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using UnitTest.ServiceHosts;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UnitTest
{

    [TestClass]
    public class NormalTest
    {
        public int _gateWayPort = 9800;
        public int _gateWayPortCert = 9805;
        public int _jmsWebapiPort = 9806;
        public int _clusterGateWayPort1 = 10001;
        public int _clusterGateWayPort2 = 10002;
        public int _UserInfoServicePort = 9801;
        public int _CrashServicePort = 9802;
        public int _UserInfoServicePort_forcluster = 9803;
        public int _webApiDocumentPort = 9901;
        public int _webApiServicePort = 9902;
        public bool _userInfoServiceReady = false;

        Gateway _clusterGateway1;
        Gateway _clusterGateway2;

        WebApplication StartWebApiService(int gateWayPort)
        {
            var builder = WebApplication.CreateBuilder(new string[] { "--urls", "http://*:" + _webApiServicePort });
            builder.Services.AddControllers();
            var gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", gateWayPort) };


            builder.Services.AddMvcCore()
.ConfigureApplicationPartManager(manager =>
{
    var featureProvider = new MyFeatureProvider();
    manager.FeatureProviders.Add(featureProvider);
});
            builder.Services.RegisterJmsService("http://127.0.0.1:" + _webApiServicePort, "TestWebService", gateways, true, option =>
            {
                option.RetryCommitPath += "9902";
            });
            var app = builder.Build();

            app.UseAuthentication();    //认证
            app.UseAuthorization();     //授权

            app.UseJmsService();

            app.MapControllers();
            return app;
        }

        public WebApplication StartWebApiDocument(int gateWayPort)
        {
            var conbuilder = new ConfigurationBuilder();
            conbuilder.AddJsonFile("./serviceConfig.json", optional: true, reloadOnChange: true);
            var configuration = conbuilder.Build();

            var builder = WebApplication.CreateBuilder(new string[] { "--urls", "http://*:" + _webApiDocumentPort });
            builder.Services.AddControllers();
            var gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", gateWayPort) };

            var app = builder.Build();

            app.UseAuthentication();    //认证
            app.UseAuthorization();     //授权

            app.UseJmsServiceRedirect(() =>
            {
                var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = gateWayPort
                   }
                };

                return new RemoteClient(gateways);
            });

            app.MapControllers();
            return app;
        }

        public void StartGateway()
        {
            Task.Run(() =>
            {

                var gatewayBuilder = GatewayBuilder.Create(new string[] { "-s:appsettings-gateway.json" });
                var gateway = gatewayBuilder.Build();
                var gatewayEnvironment = gateway.ServiceProvider.GetService<IGatewayEnvironment>();
                gatewayEnvironment.Port = _gateWayPort;
                gateway.Run();
            });
        }

        public WebApiHost StartJmsWebApi()
        {
            var webapiBuilder = WebApiHostBuilder.Create(new string[] { "-s:appsettings-webapi.json" });
            var webapi = webapiBuilder.Build();
            var webapiEnvironment = webapi.ServiceProvider.GetService<IWebApiHostEnvironment>();
            webapiEnvironment.Port = _jmsWebapiPort;
            Task.Run(() =>
            {

                webapi.Run();
            });
            return webapi;
        }

        public void StartGatewayWithCert()
        {
            Task.Run(() =>
            {
                var gatewayBuilder = GatewayBuilder.Create(new string[] { "-s:appsettings-gateway-cert.json" });
                var gateway = gatewayBuilder.Build();
                var gatewayEnvironment = gateway.ServiceProvider.GetService<IGatewayEnvironment>();
                gatewayEnvironment.Port = _gateWayPortCert;
                gateway.Run();
            });
        }

        public Gateway StartGateway_Cluster1()
        {
            var gatewayBuilder = GatewayBuilder.Create(new string[] { "-s:appsettings-gateway - cluster1.json" });
            var gateway = gatewayBuilder.Build();
            var gatewayEnvironment = gateway.ServiceProvider.GetService<IGatewayEnvironment>();
            gatewayEnvironment.Port = _clusterGateWayPort1;
            Task.Run(() =>
            {

                gateway.Run();
            });
            return gateway;
        }

        public Gateway StartGateway_Cluster2()
        {
            var gatewayBuilder = GatewayBuilder.Create(new string[] { "-s:appsettings-gateway - cluster2.json" });
            var gateway = gatewayBuilder.Build();
            var gatewayEnvironment = gateway.ServiceProvider.GetService<IGatewayEnvironment>();
            gatewayEnvironment.Port = _clusterGateWayPort2;
            Task.Run(() =>
            {

                gateway.Run();
            });
            return gateway;
        }

        public void WaitGatewayReady(int port)
        {
            //等待网关就绪
            while (true)
            {
                try
                {
                    var client = new NetClient();
                    client.Connect(new NetAddress("127.0.0.1", port));
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
                    loggingBuilder.AddDebug();
                    loggingBuilder.AddConsole(); // 将日志输出到控制台
                    loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                });

                services.AddScoped<UserInfoDbContext>();
                var msp = new MicroServiceHost(services);
                msp.RetryCommitPath = "./$$JMS_RetryCommitPath" + _UserInfoServicePort;
                //msp.ClientCheckCodeFile = "./code1.txt";
                msp.Register<TestUserInfoController>("UserInfoService", "用户服务", true);
                msp.Register<TestScopeController>("TestScopeService", "作用域测试服务", true);
                msp.Register<TestWebSocketController>("TestWebSocketService");
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
                    loggingBuilder.AddDebug();
                    loggingBuilder.AddConsole(); // 将日志输出到控制台
                    loggingBuilder.SetMinimumLevel(LogLevel.Debug);
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
                    loggingBuilder.AddDebug();
                    loggingBuilder.AddConsole(); // 将日志输出到控制台
                    loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                });

                services.AddScoped<UserInfoDbContext>();
                var msp = new MicroServiceHost(services);
                msp.RetryCommitPath = "./$$JMS_RetryCommitPath" + port;

                try
                {
                    Directory.Delete(msp.RetryCommitPath, true);
                }
                catch (Exception)
                {

                }

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
        public void AsyncDefine()
        {
            ServiceCollection services = new ServiceCollection();

            var msp = new MicroServiceHost(services);
            try
            {
                msp.Register<AsyncDemoController>("AsyncDemo");
                throw new Exception("error");
            }
            catch (MethodDefineException)
            {
            }
        }

        [TestMethod]
        public void WebapiRedirect()
        {
            StartGateway();
            StartJmsWebApi();
            StartUserInfoServiceHost();

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

            var app = StartWebApiDocument(_gateWayPort);
            app.RunAsync();

            //启动asp.net类型的微服务
            var app2 = StartWebApiService(_gateWayPort);
            app2.RunAsync();

            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = _gateWayPort
                   }
                };

            using (var rc = new RemoteClient(gateways))
            {
                var serviceClient = rc.TryGetMicroService("UserInfoService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = rc.TryGetMicroService("UserInfoService");
                }

                serviceClient = rc.TryGetMicroService("TestWebService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = rc.TryGetMicroService("TestWebService");
                }
            }

            string text;
            ClientWebSocket clientWebsocket = new ClientWebSocket();
            clientWebsocket.Options.SetRequestHeader("X-Forwarded-For", "::1");
            clientWebsocket.ConnectAsync(new Uri($"ws://localhost:{_webApiDocumentPort}/JMSRedirect/TestWebSocketService?q=100&name={HttpUtility.UrlEncode("你好")}"), CancellationToken.None).GetAwaiter().GetResult();

            StringBuilder moretext = new StringBuilder();
            for (int i = 0; i < 5000; i++)
            {
                moretext.Append("a");
            }

            for (int i = 0; i < 10; i++)
            {
                clientWebsocket.SendString($"hello{i} {moretext}").GetAwaiter().GetResult();
                text = clientWebsocket.ReadString().GetAwaiter().GetResult();
                Assert.AreEqual($"hello{i} {moretext} 你好 back", text);
            }

            clientWebsocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).GetAwaiter().GetResult();



            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            HttpResponseMessage? ret = null;
            for (int i = 0; i < 5; i++)
            {
                List<KeyValuePair<string, string>> param = new List<KeyValuePair<string, string>>();
                param.Add(new KeyValuePair<string, string>("name", "jack"));
                param.Add(new KeyValuePair<string, string>("age", "1"));

                //通过webapi反向代理访问webapi微服务
                ret = client.PostAsync($"http://localhost:{_webApiDocumentPort}/JMSRedirect/TestWebService/WeatherForecast", new FormUrlEncodedContent(param)).ConfigureAwait(false).GetAwaiter().GetResult();
                if (ret.IsSuccessStatusCode == false)
                    throw new Exception("http访问失败");
                text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                if (text != "jack1")
                    throw new Exception("http返回结果错误");

                //测试一下上传文件
                string Boundary = "EAD567A8E8524B2FAC2E0628ABB6DF6E";
                var requestContent = new MultipartFormDataContent(Boundary);
                requestContent.Headers.Remove("Content-Type");
                requestContent.Headers.TryAddWithoutValidation("Content-Type", $"multipart/form-data; boundary={Boundary}");
                var fileContent = File.ReadAllBytes("./serviceConfig.json");
                var byteArrayContent = new ByteArrayContent(fileContent);
                byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                requestContent.Add(byteArrayContent, "avatar", "Unbenannt.PNG");

                ret = client.PutAsync($"http://localhost:{_webApiDocumentPort}/JMSRedirect/TestWebService/WeatherForecast", requestContent).ConfigureAwait(false).GetAwaiter().GetResult();
                if (ret.IsSuccessStatusCode == false)
                    throw new Exception("http访问失败");
                text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }

            //用同一个连接，通过webapi反向代理访问webapi微服务
            JMS.NetClient netclient = new NetClient();
            netclient.Connect(new NetAddress("localhost", _webApiDocumentPort));
            for (int i = 0; i < 10; i++)
            {
                netclient.WriteLine("GET /JMSRedirect/TestWebService/WeatherForecast HTTP/1.1");
                netclient.WriteLine("Host: localhost");
                netclient.WriteLine("");

                Dictionary<string, string> headers = new Dictionary<string, string>();
                var content = ReadHeaders(null, netclient, headers).GetAwaiter().GetResult();
                if (!content.StartsWith("HTTP/1.1 200 OK"))
                    throw new Exception("结果不对");
                if (content.Contains("Transfer-Encoding: chunked") == false)
                    throw new Exception("结果不对");
            }
            netclient.Dispose();

            //通过网关反向代理访问webapi
            netclient = new NetClient();
            netclient.Connect(new NetAddress("localhost", _gateWayPort));
            for (int i = 0; i < 3; i++)
            {
                netclient.WriteLine("GET /UserInfoService/GetMyName HTTP/1.1");
                netclient.WriteLine("Host: localhost");
                netclient.WriteLine("");

                Dictionary<string, string> headers = new Dictionary<string, string>();
                var content = ReadHeaders(null, netclient, headers).GetAwaiter().GetResult();
                if (!content.StartsWith("HTTP/1.1 200 OK"))
                    throw new Exception("结果不对");
                if (content.Contains("Content-Length: 4") == false)
                    throw new Exception("结果不对");

                netclient.WriteLine("GET /TestWebService/WeatherForecast HTTP/1.1");
                netclient.WriteLine("Host: localhost");
                netclient.WriteLine("");

                headers = new Dictionary<string, string>();
                content = ReadHeaders(null, netclient, headers).GetAwaiter().GetResult();
                if (!content.StartsWith("HTTP/1.1 200 OK"))
                    throw new Exception("结果不对");
                if (content.Contains("Transfer-Encoding: chunked") == false)
                    throw new Exception("结果不对");

            }
            netclient.Dispose();

            //通过jmswebapi反向代理访问webapi
            netclient = new NetClient();
            netclient.Connect(new NetAddress("localhost", _jmsWebapiPort));
            for (int i = 0; i < 3; i++)
            {
                netclient.WriteLine("GET /UserInfoService/GetMyName HTTP/1.1");
                netclient.WriteLine("Host: localhost");
                netclient.WriteLine("");

                Dictionary<string, string> headers = new Dictionary<string, string>();
                var content = ReadHeaders(null, netclient, headers).GetAwaiter().GetResult();
                if (!content.StartsWith("HTTP/1.1 200 OK"))
                    throw new Exception("结果不对");
                if (content.Contains("Content-Length: 4") == false)
                    throw new Exception("结果不对");

                netclient.WriteLine("GET /TestWebService/WeatherForecast HTTP/1.1");
                netclient.WriteLine("Host: localhost");
                netclient.WriteLine("");

                headers = new Dictionary<string, string>();
                content = ReadHeaders(null, netclient, headers).GetAwaiter().GetResult();
                if (!content.StartsWith("HTTP/1.1 200 OK"))
                    throw new Exception("结果不对");
                if (content.Contains("Transfer-Encoding: chunked") == false)
                    throw new Exception("结果不对");

            }
            netclient.Dispose();


            ret = client.GetAsync($"http://localhost:{_jmsWebapiPort}/UserInfoService/GetMyNameV2?find=1&params=[\"Jack\"]&hel=2").ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == false)
                throw new Exception("http访问失败");
            text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (text != "Jack")
                throw new Exception("http返回结果错误");

            ret = client.GetAsync($"http://localhost:{_webApiDocumentPort}/JMSRedirect/UserInfoService/GetMyName").ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == false)
                throw new Exception("http访问失败");
            text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (text != "Jack")
                throw new Exception("http返回结果错误");

            ret = client.GetAsync($"http://localhost:{_webApiDocumentPort}/JMSRedirect/UserInfoService/GetMyNameError").ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode)
                throw new Exception("IsSuccessStatusCode不应该是true");
            text = ret.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (text != "ErrMsg")
                throw new Exception("http返回结果错误");

            //测试返回值是null的情况
            ret = client.GetAsync($"http://localhost:{_webApiDocumentPort}/JMSRedirect/UserInfoService/Nothing").ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == false)
                throw new Exception("http访问失败");

            ret = client.GetAsync($"http://localhost:{_jmsWebapiPort}/UserInfoService/Nothing").ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == false)
                throw new Exception("http访问失败");

            ret = client.GetAsync($"http://localhost:{_gateWayPort}/UserInfoService/Nothing").ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == false)
                throw new Exception("http访问失败");


            //测试超过文件大小 
            var jcontent = new StringContent(Encoding.UTF8.GetString(new byte[102401]), Encoding.UTF8, "application/json");

            ret = client.PostAsync($"http://localhost:{_jmsWebapiPort}/UserInfoService/GetMyNameV2", jcontent).ConfigureAwait(false).GetAwaiter().GetResult();
            if (ret.IsSuccessStatusCode == true || (int)ret.StatusCode != 413)
                throw new Exception("不应该通过");

        }

        [TestMethod]
        public void Sse()
        {
            StartGateway();
            StartJmsWebApi();
            StartUserInfoServiceHost();

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

            var app = StartWebApiDocument(_gateWayPort);
            app.RunAsync();

            //启动asp.net类型的微服务
            var app2 = StartWebApiService(_gateWayPort);
            app2.RunAsync();


            if (true)
            {
                //测试jms webapi
                string url = $"http://127.0.0.1:{_jmsWebapiPort}/TestWebService/MyWeatherForecast/SseExample"; // SSE endpoint URL

                using (var client = new System.Net.Http.HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Accept", "text/event-stream");

                    using (HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var responseStream = response.Content.ReadAsStream())
                        using (var reader = new System.IO.StreamReader(responseStream))
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                string line = reader.ReadLine();
                                reader.ReadLine();

                                Assert.AreEqual(line, $"data: {i}");

                            }
                        }
                    }
                }
            }

            if (true)
            {
                //测试jms webapidocument
                string url = $"http://127.0.0.1:{_webApiDocumentPort}/JMSRedirect/TestWebService/MyWeatherForecast/SseExample"; // SSE endpoint URL

                using (var client = new System.Net.Http.HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Accept", "text/event-stream");

                    using (HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var responseStream = response.Content.ReadAsStream())
                        using (var reader = new System.IO.StreamReader(responseStream))
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                string line = reader.ReadLine();
                                reader.ReadLine();

                                Assert.AreEqual(line, $"data: {i}");

                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void Chunked()
        {
            StartGateway();
            StartJmsWebApi();
            StartUserInfoServiceHost();

            //等待网关就绪
            WaitGatewayReady(_gateWayPort);

            var app = StartWebApiDocument(_gateWayPort);
            app.RunAsync();

            //启动asp.net类型的微服务
            var app2 = StartWebApiService(_gateWayPort);
            app2.RunAsync();


            if (true)
            {
                //测试jms webapi
                string url = $"http://127.0.0.1:{_jmsWebapiPort}/TestWebService/MyWeatherForecast/ChunkedExample";

                using (var client = new System.Net.Http.HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);

                    using (HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var responseStream = response.Content.ReadAsStream())
                        using (var reader = new System.IO.StreamReader(responseStream))
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                string line = reader.ReadLine();
                                reader.ReadLine();

                                Assert.AreEqual(line, $"data: {i}");

                            }
                        }
                    }
                }
            }

            if (true)
            {
                //测试jms webapidocument
                string url = $"http://127.0.0.1:{_webApiDocumentPort}/JMSRedirect/TestWebService/MyWeatherForecast/ChunkedExample";

                using (var client = new System.Net.Http.HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);

                    using (HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var responseStream = response.Content.ReadAsStream())
                        using (var reader = new System.IO.StreamReader(responseStream))
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                string line = reader.ReadLine();
                                reader.ReadLine();

                                Assert.AreEqual(line, $"data: {i}");

                            }
                        }
                    }
                }
            }
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

            UserInfoDbContext.Reset();
            using (var client = new RemoteClient(gateways))
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
        public void CommitAsync()
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

            UserInfoDbContext.Reset();
            using (var client = new RemoteClient(gateways))
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

                client.CommitTransactionAsync().Wait();
            }

            Debug.WriteLine($"结果：{UserInfoDbContext.FinallyUserName}");

            if (UserInfoDbContext.FinallyUserName != "Jack" ||
                UserInfoDbContext.FinallyAge != 28 ||
                UserInfoDbContext.FinallyFather != "Tom" ||
                UserInfoDbContext.FinallyMather != "Lucy")
                throw new Exception("结果不正确");
        }

        [TestMethod]
        public void TestScope()
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

            UserInfoDbContext.Reset();
            using (var client = new RemoteClient(gateways))
            {
                var serviceClient = client.TryGetMicroService("TestScopeService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("TestScopeService");
                }

                client.BeginTransaction();
                serviceClient.InvokeAsync("SetFather", "Tom");
                serviceClient.InvokeAsync("SetUserName", "Jack1");
                serviceClient.InvokeAsync("SetUserName", "Jack");
                serviceClient.InvokeAsync("SetAge", 28);

                client.CommitTransactionAsync().Wait();
            }

            Debug.WriteLine($"结果：{UserInfoDbContext.FinallyUserName}");

            if (UserInfoDbContext.NewInstanceCount != 1)
            {
                throw new Exception($"new了{UserInfoDbContext.NewInstanceCount}次");
            }
            if (UserInfoDbContext.CommitCount != 1)
            {
                throw new Exception($"commit了{UserInfoDbContext.CommitCount}次");
            }
            if (UserInfoDbContext.FinallyUserName != "Jack" ||
                UserInfoDbContext.FinallyFather != "Tom" ||
                UserInfoDbContext.FinallyAge != 28)
                throw new Exception("结果不正确");
        }

        [TestMethod]
        public void TestLockKey()
        {
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

            UserInfoDbContext.Reset();
            using (var client = new RemoteClient(gateways))
            {
                var serviceClient = client.TryGetMicroService("TestScopeService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("TestScopeService");
                }

                serviceClient.Invoke("TestLockKey");
            }

            using (var client = new RemoteClient(gateways))
            {
                var serviceClient = client.TryGetMicroService("TestScopeService");

                serviceClient.Invoke("TestLockScopedKey");
                serviceClient.Invoke("TestLockScopedKey");
            }

            using (var client = new RemoteClient(gateways))
            {
                var serviceClient = client.TryGetMicroService("TestScopeService");

                serviceClient.Invoke("TestLockScopedKey");
                serviceClient.Invoke("TestLockScopedKey");
            }
        }

        /// <summary>
        /// 测试是否优先选择已有的服务器
        /// </summary>
        /// <exception cref="Exception"></exception>
        [TestMethod]
        public void TestGetSameLocation()
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

            UserInfoDbContext.Reset();
            using (var client = new RemoteClient(gateways))
            {
                var serviceClient = client.TryGetMicroService("TestScopeService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("TestScopeService");
                }

                var serviceClient2 = client.TryGetMicroService("UserInfoService");
                var serviceClient3 = client.TryGetMicroServiceAsync("TestScopeService").GetAwaiter().GetResult();
            }

        }

        [TestMethod]
        public void TestCrashScope()
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

            var transactionId = "";
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
                serviceClient.Invoke("SetAge", 28);
                crashService.Invoke("SetText", "abc1");
                crashService.Invoke("SetText", "abc");
                try
                {
                    transactionId = client.TransactionId;
                    client.CommitTransaction();
                }
                catch (Exception ex)
                {

                    Debug.WriteLine(ex.Message);
                }

            }

            Thread.Sleep(12000);//等待7秒，失败的事务

            if (UserInfoDbContext.FinallyUserName != "Jack" || UserInfoDbContext.FinallyAge != 28 || TestCrashController.FinallyText != "abc")
                throw new Exception("结果不正确");
        }


        /// <summary>
        /// 由RemoteClient发起重试请求
        /// </summary>
        /// <exception cref="Exception"></exception>
        [TestMethod]
        public void TestCrashScopeForLocal()
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

            ServiceCollection services = new ServiceCollection();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddDebug();
                loggingBuilder.AddConsole(); // 将日志输出到控制台
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            });
            var serviceProvider = services.BuildServiceProvider();

            string tranid;
            using (var client = new RemoteClient(gateways, null, serviceProvider.GetService<ILogger<RemoteClient>>()))
            {
                var serviceClient = client.TryGetMicroService("TestScopeService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("TestScopeService");
                }

                var crashService = client.GetMicroService("CrashService", new JMS.Dtos.ClientServiceDetail("127.0.0.1", _CrashServicePort));

                client.BeginTransaction();
                tranid = client.TransactionId;

                serviceClient.Invoke("SetUserName", "Jack");
                serviceClient.Invoke("SetAge", 28);
                crashService.Invoke("SetText", "abc1");
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
            DateTime starttime = DateTime.Now;
            while (File.Exists($"./$$_JMS.Invoker.Transactions/{tranid}.json"))
            {
                if ((DateTime.Now - starttime).TotalSeconds > 80)
                    throw new Exception("超时");
                Thread.Sleep(1000);
            }

            if (UserInfoDbContext.FinallyUserName != "Jack" || UserInfoDbContext.FinallyAge != 28 || TestCrashController.FinallyText != "abc")
                throw new Exception("结果不正确");

            ThreadPool.GetAvailableThreads(out int w, out int c);
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

            if (UserInfoDbContext.FinallyUserName != null ||
                UserInfoDbContext.FinallyAge != 0 ||
                UserInfoDbContext.FinallyFather != null ||
                UserInfoDbContext.FinallyMather != null)
                throw new Exception("结果不正确");
        }

        [TestMethod]
        public void ValueTaskAndTask()
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

                Assert.AreEqual(serviceClient.Invoke<string>("GetValueTaskName"), "ValueTask");
                Assert.AreEqual(serviceClient.Invoke<string>("GetTaskName"), "Task");
            }

        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        [TestMethod]
        public void HttpsTest()
        {
            StartGatewayWithCert();

            //等待网关就绪
            WaitGatewayReady(_gateWayPortCert);

            JMS.NetClient client = new JMS.NetClient();
            client.Connect(new NetAddress("127.0.0.1", _gateWayPortCert));
            client.AsSSLClient("127.0.0.1", RemoteCertificateValidationCallback);
            var content = @"GET /?GetAllServiceProviders HTTP/1.1
Host: 127.0.0.1
Connection: keep-alive
User-Agent: JmsInvoker
Accept: text/html
Accept-Encoding: deflate, br
Accept-Language: zh-CN,zh;q=0.9
Content-Length: 0

";

            for (int i = 0; i < 2; i++)
            {

                client.Write(Encoding.UTF8.GetBytes(content));

                byte[] data = new byte[40960];
                var len = client.InnerStream.Read(data, 0, data.Length);
                var text = Encoding.UTF8.GetString(data, 0, len);
            }
            client.Dispose();

            if (true)
            {
                JMS.CertClient client2 = new JMS.CertClient();
                client2.Connect(new NetAddress("127.0.0.1", _gateWayPortCert, new X509Certificate2("../../../../pfx/client.pfx", "123456")));

                client2.Write(Encoding.UTF8.GetBytes(content));

                var data = new byte[40960];
                var len = client2.InnerStream.Read(data, 0, data.Length);
                var text = Encoding.UTF8.GetString(data, 0, len);
                client2.Dispose();
            }
        }

        [TestMethod]
        public void RollbackForError()
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

                    var crashService = client.TryGetMicroService("CrashService");
                    while (crashService == null)
                    {
                        Thread.Sleep(10);
                        crashService = client.TryGetMicroService("CrashService");
                    }

                    client.BeginTransaction();
                    serviceClient.Invoke("SetUserName", "Jack");
                    serviceClient.Invoke("SetAge", 28);

                    crashService.InvokeAsync("SetText", "Tom");

                    serviceClient.InvokeAsync("SetFather", "Tom");
                    serviceClient.InvokeAsync("SetMather", "Lucy");
                    serviceClient.InvokeAsync("BeError");//这个方法调用会有异常
                    client.CommitTransaction();

                }


            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;
                string msg = ex.Message;
                if (msg != "有意触发错误")
                    throw ex;
            }

            bool hasNewClient = false;
            NetClientPool.CreatedNewClient += (s, e) =>
            {
                hasNewClient = true;
            };
            //下面测试一下连接池是否正常
            using (var client = new RemoteClient(gateways))
            {
                var crashService = client.TryGetMicroService("CrashService");
                if (crashService.Invoke<string>("NoTran", "Tom") != "Tom")
                {
                    throw new Exception("结果错误");
                }
            }
            if (hasNewClient)
                throw new Exception("创建了新的连接");

            if (UserInfoDbContext.FinallyUserName != null ||
                UserInfoDbContext.FinallyAge != 0 ||
                UserInfoDbContext.FinallyFather != null ||
                UserInfoDbContext.FinallyMather != null)
                throw new Exception("结果不正确");
        }

        [TestMethod]
        public void RollbackForHttpResultError()
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

                    var crashService = client.TryGetMicroService("CrashService");
                    while (crashService == null)
                    {
                        Thread.Sleep(10);
                        crashService = client.TryGetMicroService("CrashService");
                    }

                    client.BeginTransaction();
                    serviceClient.Invoke("SetUserName", "Jack");
                    serviceClient.Invoke("SetAge", 28);

                    crashService.InvokeAsync("SetText", "Tom");

                    serviceClient.InvokeAsync("SetFather", "Tom");
                    serviceClient.InvokeAsync("SetMather", "Lucy");
                    serviceClient.InvokeAsync("BeHttpError");//这个方法会用HttpResult方式返回错误，所以这里会抛出异常
                    client.CommitTransaction();

                }


            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;
                string msg = ex.Message;
                if (msg != "有意触发错误")
                    throw ex;
            }

            bool hasNewClient = false;
            NetClientPool.CreatedNewClient += (s, e) =>
            {
                hasNewClient = true;
            };
            //下面测试一下连接池是否正常
            using (var client = new RemoteClient(gateways))
            {
                var crashService = client.TryGetMicroService("CrashService");
                if (crashService.Invoke<string>("NoTran", "Tom") != "Tom")
                {
                    throw new Exception("结果错误");
                }
            }
            if (hasNewClient)
                throw new Exception("创建了新的连接");

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

                using (var otherclient = new RemoteClient("127.0.0.2", 1))
                {
                    var testservice = otherclient.GetMicroService("UserInfoService", serviceClient.ServiceLocation);
                    testservice = otherclient.GetMicroServiceAsync("UserInfoService", serviceClient.ServiceLocation).GetAwaiter().GetResult();
                }


                serviceClient.Invoke("SetUserName", "Jack");
                serviceClient.Invoke("SetAge", 28);
                serviceClient.InvokeAsync("SetFather", "Tom");
                serviceClient.InvokeAsync("SetMather", "Lucy").GetAwaiter().GetResult();
            }

            Debug.WriteLine($"结果：{UserInfoDbContext.FinallyUserName}");

            if (UserInfoDbContext.FinallyUserName != "Jack" ||
                UserInfoDbContext.FinallyAge != 28 ||
                UserInfoDbContext.FinallyFather != "Tom" ||
                UserInfoDbContext.FinallyMather != "Lucy")
                throw new Exception($"结果不正确, {UserInfoDbContext.FinallyUserName} {UserInfoDbContext.FinallyAge} {UserInfoDbContext.FinallyFather} {UserInfoDbContext.FinallyMather}");
        }

        [TestMethod]
        public void JsonTest()
        {
            InvokeCommand cmdObj = new InvokeCommand() { 
                Method = "aa",
            };

            var json = cmdObj.ToJsonString();
            Assert.AreEqual(json, "{\"Type\":0,\"Header\":{},\"Method\":\"aa\"}");


            var invokeCommand = @"{""Type"":""2""}".FromJson<InvokeCommand>();
            Assert.AreEqual(invokeCommand.Type, 2);

            var invokeResult = @"{""data"":""2"" , ""Success"":true}".FromJson<InvokeResult<int>>();
            Assert.AreEqual(invokeResult.Data, 2);

            var invokeResult2 = @"{""Data"":""2020-01-01"" }".FromJson<InvokeResult<DateTime>>();

            var cmd = @"{""Content"":{""Data"":""2020-01-01"",""ar"":[1,{""Type"":""2""},3] },""Type"":""2"" }".FromJson<GatewayCommand>();
            Assert.AreEqual(cmd.Content, null);
            Assert.AreEqual(cmd.Type, 2);
        }

        /// <summary>
        /// 测试提交时，有个服务宕机
        /// </summary>
        /// <exception cref="Exception"></exception>
        [TestMethod]
        public void TestCrash()
        {
            TestCrashController.CanCrash = true;
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
                crashService.Invoke("SetTextWithUserContent", "abc");
                try
                {
                    client.CommitTransaction();
                }
                catch (Exception ex)
                {

                    Debug.WriteLine(ex.Message);
                }

            }

            Thread.Sleep(12000);//等待7秒，失败的事务

            if (UserInfoDbContext.FinallyUserName != "Jack" || TestCrashController.FinallyText != "abc")
                throw new Exception("结果不正确");
        }

        /// <summary>
        /// 由RemoteClient发起重试请求
        /// </summary>
        /// <exception cref="Exception"></exception>
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

            ServiceCollection services = new ServiceCollection();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddDebug();
                loggingBuilder.AddConsole(); // 将日志输出到控制台
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            });
            var serviceProvider = services.BuildServiceProvider();

            string tranid;
            using (var client = new RemoteClient(gateways, null, serviceProvider.GetService<ILogger<RemoteClient>>()))
            {
                var serviceClient = client.TryGetMicroService("UserInfoService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("UserInfoService");
                }

                var crashService = client.GetMicroService("CrashService", new JMS.Dtos.ClientServiceDetail("127.0.0.1", _CrashServicePort));

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
            DateTime starttime = DateTime.Now;
            while (File.Exists($"./$$_JMS.Invoker.Transactions/{tranid}.json"))
            {
                if ((DateTime.Now - starttime).TotalSeconds > 80)
                    throw new Exception("超时");
                Thread.Sleep(1000);
            }

            if (UserInfoDbContext.FinallyUserName != "Jack" || TestCrashController.FinallyText != "abc")
                throw new Exception("结果不正确");

            ThreadPool.GetAvailableThreads(out int w, out int c);
        }


        /// <summary>
        /// 测试网关集群
        /// </summary>
        [TestMethod]
        public void TestGatewayCluster()
        {
            UserInfoDbContext.Reset();
            _clusterGateway1 = StartGateway_Cluster1();
            _clusterGateway2 = StartGateway_Cluster2();

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

            var serviceProvider1 = _clusterGateway1.ServiceProvider;
            var clusterGatewayConnector1 = serviceProvider1.GetService<ClusterGatewayConnector>();

            var serviceProvider2 = _clusterGateway2.ServiceProvider;
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

                serviceClient.Invoke("LockName", "abc", "d", "e", "f");

                if (lockManager.GetAllKeys().Any(k => k.Key == "abc") == false)
                {
                    throw new Exception("找不到lock key");
                }
                serviceClient.Invoke("UnlockName", "d");
            }

            var slaveGateway = clusterGatewayConnector1.IsMaster ? _clusterGateway2 : _clusterGateway1;
            var slaveLockManager = (clusterGatewayConnector1.IsMaster ? serviceProvider2 : serviceProvider1).GetService<LockKeyManager>();
            while (slaveLockManager.GetAllKeys().Any(k => k.Key == "f") == false)
            {
                Thread.Sleep(1000);
            }

            //关闭主网关
            masterGateway.Dispose();

            var serviceProvider = slaveGateway.ServiceProvider;
            var clusterGatewayConnector = serviceProvider.GetService<ClusterGatewayConnector>();

            //等待从网关成为主网关
            while (clusterGatewayConnector.IsMaster == false)
            {
                Thread.Sleep(100);
            }

            while (slaveLockManager.GetAllKeys().Any(m => m.RemoveTime != null))
            {
                Thread.Sleep(100);
            }
            if (slaveLockManager.GetAllKeys().Length != 3)
            {
                throw new Exception("lock key数量不对");
            }
        }

        [TestMethod]
        public void TestWebsocket()
        {
            UserInfoDbContext.Reset();
            StartGateway();
            StartJmsWebApi();
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
                var serviceClient = client.TryGetMicroService("TestWebSocketService");
                while (serviceClient == null)
                {
                    Thread.Sleep(10);
                    serviceClient = client.TryGetMicroService("TestWebSocketService");
                }
            }

            var serverPorts = new int[] { _gateWayPort, _jmsWebapiPort };

            foreach (var port in serverPorts)
            {
                var clientWebsocket = new ClientWebSocket();
                clientWebsocket.Options.AddSubProtocol("chat");
                clientWebsocket.Options.AddSubProtocol("jjdoc");
                clientWebsocket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/TestWebSocketService?name=test"), CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
                var text = clientWebsocket.ReadString().ConfigureAwait(true).GetAwaiter().GetResult();
                if (text != "hello")
                    throw new Exception("error");

                text = clientWebsocket.ReadString().ConfigureAwait(true).GetAwaiter().GetResult();
                if (text != "test")
                    throw new Exception("获取不到query");

                clientWebsocket.SendString("test").ConfigureAwait(true).GetAwaiter().GetResult();
                text = clientWebsocket.ReadString().ConfigureAwait(true).GetAwaiter().GetResult();
                if (text != "test")
                    throw new Exception("error");

                try
                {
                    text = clientWebsocket.ReadString().ConfigureAwait(true).GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                }


                if (clientWebsocket.CloseStatus != WebSocketCloseStatus.NormalClosure)
                    throw new Exception("error");

                if (clientWebsocket.CloseStatusDescription != "abc")
                    throw new Exception("error");
            }
        }

        [TestMethod]
        public void GetRemoteIp()
        {
            string remoteIp = "127.0.0.1";
            string[] trustIps = new[] { "192.168.0.1" };
            string xForwardedfor = "166.0.0.1, 127.0.0.1, 192.168.0.1";

            var ip = RequestTimeLimter.GetRemoteIpAddress(trustIps, remoteIp, xForwardedfor);
            Assert.AreEqual(ip, "166.0.0.1");

            remoteIp = "166.0.0.2";
            trustIps = new[] { "192.168.0.1" };
            xForwardedfor = "166.0.0.1, 127.0.0.1, 192.168.0.1";

            ip = RequestTimeLimter.GetRemoteIpAddress(trustIps, remoteIp, xForwardedfor);
            Assert.AreEqual(ip, "166.0.0.2");

            remoteIp = "127.0.0.1";
            trustIps = new[] { "192.168.0.1" };
            xForwardedfor = "127.0.0.1, 166.0.0.1, 192.168.0.1";

            ip = RequestTimeLimter.GetRemoteIpAddress(trustIps, remoteIp, xForwardedfor);
            Assert.AreEqual(ip, "166.0.0.1");
        }


        [TestMethod]
        public void TestIgnoreCaseHeader()
        {
            var c = "{ \"Headers\":{\"a\":\"a23\",\"b\":\"caw\"} }".FromJson<ClassA>();
            Assert.AreEqual(c.Headers["A"], "a23");
            Assert.AreEqual( c.Headers["B"] , "caw");
        }

        class ClassA
        {
            public IgnoreCaseDictionary Headers ;
        }

        public static async Task<string> ReadHeaders(string preRequestString, NetClient client, IDictionary<string, string> headers)
        {
            List<byte> lineBuffer = new List<byte>(1024);
            string line = null;
            string requestPathLine = null;
            byte[] bData = new byte[1];
            int readed;
            while (true)
            {
                readed = await client.InnerStream.ReadAsync(bData, 0, 1);
                if (readed <= 0)
                    throw new SocketException();

                if (bData[0] == 10)
                {
                    line = Encoding.UTF8.GetString(lineBuffer.ToArray());
                    lineBuffer.Clear();
                    if (requestPathLine == null)
                        requestPathLine = preRequestString + line;

                    if (line == "")
                    {
                        break;
                    }
                    else if (line.Contains(":"))
                    {
                        var arr = line.Split(':');
                        if (arr.Length >= 2)
                        {
                            var key = arr[0].Trim();
                            var value = arr[1].Trim();
                            if (headers.ContainsKey(key) == false)
                            {
                                headers[key] = value;
                            }
                        }
                    }
                }
                else if (bData[0] != 13)
                {
                    lineBuffer.Add(bData[0]);
                }
            }


            var inputContentLength = 0;
            if (headers.ContainsKey("Content-Length"))
            {
                int.TryParse(headers["Content-Length"], out inputContentLength);
            }

            var strBuffer = new StringBuilder();
            strBuffer.AppendLine(requestPathLine);

            foreach (var pair in headers)
            {
                strBuffer.AppendLine($"{pair.Key}: {pair.Value}");
            }

            strBuffer.AppendLine("");


            if (inputContentLength > 0)
            {
                var data = new byte[inputContentLength];
                await client.ReadDataAsync(data, 0, inputContentLength);
                strBuffer.AppendLine(Encoding.UTF8.GetString(data));
            }
            else if (headers.TryGetValue("Transfer-Encoding", out string transferEncoding) && transferEncoding == "chunked")
            {
                while (true)
                {
                    line = await client.ReadLineAsync();
                    strBuffer.AppendLine(line);

                    inputContentLength = Convert.ToInt32(line, 16);
                    if (inputContentLength == 0)
                    {
                        line = await client.ReadLineAsync();
                        strBuffer.AppendLine(line);
                        break;
                    }
                    else
                    {
                        var data = new byte[inputContentLength];
                        await client.ReadDataAsync(data, 0, inputContentLength);
                        strBuffer.AppendLine(Encoding.UTF8.GetString(data));

                        line = await client.ReadLineAsync();
                        strBuffer.AppendLine(line);
                    }
                }
            }
            return strBuffer.ToString();
        }
    }
}
