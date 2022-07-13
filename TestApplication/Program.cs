using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using JMS;
using JMS.Dtos;
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
        class Test
        {
            public Type Type;
            public string Value;
        }
        static void Main(string[] args)
        {
            while (true)
            {
                try
                {
                    var client = new NetClient("127.0.0.1", 8913);
                    client.Dispose();

                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }              
            }

       

            using (var tran = new RemoteClient(new NetAddress[] { 
                new NetAddress("localhost", 8912)
            },null,null , null, null))
            {
                tran.BeginTransaction();
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

                //var c1 = tran.GetMicroService("Service2");
                //var code22 = c1.GetServiceClassCode("a", "b");

             
                Controller1 controller1 = null;
                while (controller1 == null)
                {
                    Thread.Sleep(1000);
                    controller1 = tran.TryGetMicroService<Controller1>("100");
                }

                var Service2 = tran.GetMicroService("Service2" , "100");

                var ret = controller1.Test(123, "Jack.T");
                Console.WriteLine("调用结果：{0}", ret);

              

                controller1.Test2Async();

                var task = controller1.IntTestAsync();
                task.Wait();
                Console.WriteLine("异步调用结果：{0}", task.Result);

                ret =  Service2.Invoke<string>("GetName" , new TestObject { Age = 12});

                tran.CommitTransaction();
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
