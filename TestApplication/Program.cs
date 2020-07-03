using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using JMS;
using JMS.Token;
using Microsoft.AspNetCore.Mvc;
using Way.Lib;

namespace TestApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            //Thread.Sleep(1000);
            //var tokenclient = new TokenClient("localhost", 9911);
            //var token = tokenclient.BuildForString(new { userid = 12 , expire = 12u}.ToJsonString());
            //var body = tokenclient.VerifyForString(token);

            //token = tokenclient.BuildForLongs(new[] {12u,(long)(DateTime.Now - Convert.ToDateTime("1970-1-1")).TotalSeconds });
            //var body2 = tokenclient.VerifyForLongs(token);



            //var t = new test();
            //var type = t.GetType();
            //var method = type.GetMethod("c");
            //var p = new object[] {2 };


            //var e1 = Expression.Parameter(typeof(object), "s");
            //var e2 = Expression.Convert(e1, typeof(test));
            //var body = Expression.Call(e2, typeof(test).GetMethod("c"), Expression.Constant(2));
            //var ee2 = Expression.Lambda(body, e1);
            //var func = (Func<object, MyResut>)ee2.Compile();


            //Expression<Func<object,object>> exp = (s) => ((Func<object,int>)s)(t);

            ////web api 性能测试
            //System.Diagnostics.Stopwatch sw2 = new System.Diagnostics.Stopwatch();
            //sw2.Start();
            //for (int i = 0; i < 2000; i++)
            //{
            //    var ret = Way.Lib.HttpClient.GetContent("http://localhost:8888/home/test", 8000);
            //}
            //sw2.Stop();
            //Console.WriteLine(sw2.ElapsedMilliseconds);
            Thread.Sleep(3000);

            using (var tran = new MicroServiceTransaction("localhost", 8911))
            {
                /////微服务 性能测试
                //var controller1 = tran.GetMicroService("Controller1");
                //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                //sw.Start();
                //for (int i = 0; i < 2000; i++)
                //{
                //    var ret = controller1.Invoke<string>("test");
                //}
                //sw.Stop();
                //Console.WriteLine(sw.ElapsedMilliseconds);

                tran.Header = new Dictionary<string, string>() {
                    { "auth" , "123456789"}
                };

                var locations = tran.ListMicroService("");//列出所有微服务
                Console.WriteLine("微服务位置：{0}", locations.ToJsonString());

                var controller1 = tran.GetMicroService("Controller1");
                var Service2 = tran.GetMicroService("Service2");

                var ret = controller1.Invoke<string>("Test", 123, "Jack.T");
                Console.WriteLine("调用结果：{0}", ret);

                controller1.InvokeAsync("Test2");

                var task = controller1.InvokeAsync<long>("IntTest", 889, "Async.T");
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
