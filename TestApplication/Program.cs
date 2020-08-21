using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using JMS;
using JMS.Token;
using Newtonsoft.Json;
using Way.Lib;

namespace TestApplication
{
    class TestObject
    {
        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 子类型
        /// </summary>
        public TestObject[] Child { get; set; }

        /// <summary>
        /// 年龄
        /// </summary>
        public int Age;
    }
    class Program
    {
        static void Main(string[] args)
        {
            var tokenclient = new TokenClient("localhost", 9911);
            var token = tokenclient.BuildStringWithExpire( "123" , DateTime.Now.AddYears(1) );
            token = tokenclient.BuildLongWithExpire(100, DateTime.Now.AddYears(1));

            //token = tokenclient.BuildForLongs(new[] { 12u, (long)(DateTime.Now - Convert.ToDateTime("1970-1-1")).TotalSeconds });
            //var body2 = tokenclient.VerifyForLongs(token);


            while (true)
            {
                try
                {
                    NetClient client = new NetClient("127.0.0.1", 8911);
                    client.Dispose();
                    client = new NetClient("127.0.0.1", 8912);
                    client.Dispose();

                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }              
            }

            var cert = new X509Certificate2("../../../../pfx/client.pfx", "123456");

            using (var tran = new JMSClient(new NetAddress[] { 
                new NetAddress("localhost", 8911)
            },null,null , cert, cert))
            {
                tran.Timeout = 0;
                /////微服务 性能测试
                //var c1 = new Controller1(tran.GetMicroService("Controller1"));
                //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                //sw.Start();
                //for (int i = 0; i < 1000; i++)
                //{
                //    var ret22 = c1.test();
                //}
                //sw.Stop();
                //Console.WriteLine(sw.ElapsedMilliseconds);
                tran.SetHeader("auth", "WyJ7XCJkXCI6XCIxMjNcIixcImVcIjoxNjI5NTM4ODMxfSIsIkZFREE2MkU2RTg3RTZDNDhBOEI1NkExOURDNEUwQzQ2Il0=");
                //tran.SetHeader("auth", "AgB7AAAAAAAAAEzJIGEAAAAAOEZEMkUxODk4MjVFOEM5NzdGNjJGMEIyNkFERTBBRkQ=");

                //var locations = tran.ListMicroService("");//列出所有微服务
                //Console.WriteLine("微服务位置：{0}", locations.ToJsonString());

                var code =  tran.GetMicroService("Service2").GetServiceClassCode("TestApplication", "Controller1");

                Controller1 controller1 = null;
                while (controller1 == null)
                {
                    Thread.Sleep(1000);
                    controller1 = tran.GetMicroService<Controller1>();
                }
                var Service2 = tran.GetMicroService("Service2");

                var ret = controller1.Test(123, "Jack.T");
                Console.WriteLine("调用结果：{0}", ret);

              

                controller1.Test2Async();

                var task = controller1.IntTestAsync();
                task.Wait();
                Console.WriteLine("异步调用结果：{0}", task.Result);

                ret =  Service2.Invoke<string>("GetName" , new TestObject { Age = 12});

                tran.Commit();
            }
            Console.WriteLine("事务提交");
            Thread.Sleep(20000000);
        }
    }
    
    class MyResut
    {
        public static MyResut instance = new MyResut();
    }

    class test
    {
        public MyResut c(int a)
        {
            return MyResut.instance;
        }
    }
}
