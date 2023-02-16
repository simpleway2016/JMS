using JMS.AssemblyDocumentReader;
using JMS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using JMS.GenerateCode;
using Way.Lib;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTest
{
    public class Setting3 : Setting1
    {
        public int Age2 { get; set; }
    }
    class Setting2 : Setting1
    {
        public int Age { get; set; }
    }
    public class Setting1
    {
        public string Name { get; set; }
        public Dictionary<string,int> Dict { get; set; }
    }

    public class TClass<T>
    {
        /// <summary>
        /// 测试名称
        /// </summary>
        public T CName { get; set; }

        /// <summary>
        /// 测试枚举
        /// </summary>
        public enum Enum1
        {
            /// <summary>
            /// 正常的
            /// </summary>
            Normal = 1,
        }
    }
    public class TestModel<T1, T2>
    {
        public T2 Tag { get; set; }
        /// <summary>
        /// 测试集
        /// </summary>
        public List<T1> Datas { get; set; }
        public List<string> Datas2 { get; set; }

        /// <summary>
        /// 测试方法2
        /// </summary>
        /// <typeparam name="T1">t1注释</typeparam>
        /// <param name="name">姓名</param>
        /// <returns></returns>
        public string GetString<T1, T2>(string name)
        {
            return "";
        }
    }

    class DataModel1
    {
        public decimal? Amount { get; set; }
    }

    /// <summary>
    /// 测试controller
    /// </summary>
    class TestController : MicroServiceControllerBase
    {
        public override void OnAfterAction(string actionName, object[] parameters)
        {
            base.OnAfterAction(actionName, parameters);
        }
        public TestModel<TClass<int>, TClass<double>> Hellow()
        {
            return null;
        }
        /// <summary>
        /// Hellow2
        /// </summary>
        /// <returns></returns>
        public List<string> Hellow2()
        {
            return null;
        }

        public DataModel1 GetModel()
        {
            return null;
        }

        public Dictionary<string,string> Dict()
        {
            return null;
        }
        /// <summary>
        /// Hellow5的注释
        /// </summary>
        /// <param name="a">a的注释
        /// 不是吧</param>
        /// <param name="b2">b2的注释</param>
        /// <returns>
        /// 测试
        /// 文字
        /// </returns>
        public List<string> Hellow5(TestModel<TClass<int>, TClass<double>> a, int b2)
        {
            return null;
        }
        public System.Collections.ArrayList Hellow3()
        {
            return null;
        }
        public async Task<Setting1> Test3()
        {
            return null;
        }

        public async Task AddTemplate(long templateTypeId, string templateTypeName, Dictionary<string, string> templateContent, Dictionary<string, string> templateTitle,  string url)
        {

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
            host.Run();
            var type = typeof(MicroServiceHost).Assembly.GetType("JMS.GenerateCode.CodeBuilder");
            var builder = Activator.CreateInstance(type, new object[] { host });
            var str = type.GetMethod("GenerateCode").Invoke(builder, new object[] { "abc", "MyClass", "testService" });
        }

        [TestMethod]
        public void DocumentReaderTest()
        {
            var doc = DocumentReader.GetTypeDocument(typeof(TClass<int>.Enum1));
            doc = DocumentReader.GetTypeDocument(typeof(TestController));
        }

        [TestMethod]
        public void TypeinfoBuilderTest()
        {
            var baseMethods = typeof(MicroServiceControllerBase).GetMethods().Select(m => m.Name).ToArray();
            var typeinfo = new ControllerTypeInfo()
            {
                Service = new JMS.Dtos.ServiceDetail { 
                Name = "testService"
                },
                Type = typeof(TestController),
                Methods = typeof(TestController).GetTypeInfo().DeclaredMethods.Where(m =>
                    m.IsStatic == false &&
                    m.IsPublic &&
                    m.IsSpecialName == false &&
                    m.DeclaringType != typeof(MicroServiceControllerBase) &&
                    baseMethods.Contains(m.Name) == false &&
                    m.DeclaringType != typeof(object)).OrderBy(m => m.Name).Select(m => new TypeMethodInfo
                    {
                        Method = m,
                        NeedAuthorize = m.GetCustomAttribute<AuthorizeAttribute>() != null
                    }).ToArray()
            };
            var code = TypeInfoBuilder.Build(typeinfo).ToJsonString(true);
        }
    }
}