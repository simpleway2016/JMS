using JMS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace UnitTest
{
    public class TClass<T>
    {
        public T CName { get; set; }
    }
    public class TestModel<T1,T2>
    {
        public T2 Tag { get; set; }
        public List<T1> Datas { get; set; }
        public List<string> Datas2 { get; set; }
    }

    class TestController : MicroServiceControllerBase
    {
        public TestModel<TClass<int>, TClass<double>> Hellow()
        {
            return null;
        }
        public List<string> Hellow2()
        {
            return null;
        }
        public System.Collections.ArrayList Hellow3()
        {
            return null;
        }
    }

    [TestClass]
    public class CodeBuilderTest
    {
       [TestMethod]
       public void Test()
        {
            MicroServiceHost host = new MicroServiceHost(new ServiceCollection());
            host.Register<TestController>("testService");
            var type = typeof(MicroServiceHost).Assembly.GetType("JMS.GenerateCode.CodeBuilder");
            var builder = Activator.CreateInstance(type , new object[] {host });
            var str = type.GetMethod("GenerateCode").Invoke(builder, new object[] {"abc" , "MyClass" , "testService" });
        }
    }
}