using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using JMS;
using JMS.Common.Dtos;
using JMS.Token;
using Microsoft.AspNetCore.Mvc;
using Way.Lib;

namespace TestApplication
{
    class Program
    {
        static void Main(string[] args)
        {

            //var tokenclient = new TokenClient("localhost", 9911);
            //var token = tokenclient.BuildForString(new { userid = 12, expire = 12u }.ToJsonString());
            //var body = tokenclient.VerifyForString(token);

            //token = tokenclient.BuildForLongs(new[] { 12u, (long)(DateTime.Now - Convert.ToDateTime("1970-1-1")).TotalSeconds });
            //var body2 = tokenclient.VerifyForLongs(token);


           while(true)
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

            using (var tran = new MicroServiceTransaction(new NetAddress[] { 
                new NetAddress("localhost", 8911)
            },null,null , cert, cert))
            {
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

                tran.SetHeader("auth", "123456789");

                var locations = tran.ListMicroService("");//列出所有微服务
                Console.WriteLine("微服务位置：{0}", locations.ToJsonString());

               //var code =  tran.GetMicroService("Controller1").GetServiceClassCode("TestApplication", "Controller1");

                var controller1 = tran.GetMicroService<Controller1>();
                var Service2 = tran.GetMicroService("Service2");

                var ret = controller1.Test(123, "Jack.T");
                Console.WriteLine("调用结果：{0}", ret);

                controller1.Test2Async();

                var task = controller1.IntTestAsync();
                task.Wait();
                Console.WriteLine("异步调用结果：{0}", task.Result);

                Service2.InvokeAsync("GetName");

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
