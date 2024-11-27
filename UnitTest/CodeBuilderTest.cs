using JMS.AssemblyDocumentReader;
using JMS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using JMS.GenerateCode;

using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using JMS.Controllers;
using JMS.ServerCore.Http;

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
        /// ��������
        /// </summary>
        public T CName { get; set; }

        /// <summary>
        /// ����ö��
        /// </summary>
        public enum Enum1
        {
            /// <summary>
            /// ������
            /// </summary>
            Normal = 1,
        }
    }
    public class TestModel<T1, T2>
    {
        public T2 Tag { get; set; }
        /// <summary>
        /// ���Լ�
        /// </summary>
        public List<T1> Datas { get; set; }
        public List<string> Datas2 { get; set; }

        /// <summary>
        /// ���Է���2
        /// </summary>
        /// <typeparam name="T1">t1ע��</typeparam>
        /// <param name="name">����</param>
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
    /// ����controller
    ///     �����룺
    ///     1=������Ϣ
    /// </summary>
    class TestController : MicroServiceControllerBase
    {
        public override void OnAfterAction(string actionName, object[] parameters)
        {
            base.OnAfterAction(actionName, parameters);
        }

        public TestModel<TClass<int>, TClass<double>> Hellow(string? name)
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
        [Category("3ϵ")]
        public Dictionary<string,string> Dict()
        {
            return null;
        }
        /// <summary>
        /// Hellow5��ע��
        /// </summary>
        /// <param name="a">a��ע��
        /// ���ǰ�</param>
        /// <param name="b2">b2��ע��</param>
        /// <returns>
        /// ����
        /// ����
        /// </returns>
        public List<string> Hellow5(TestModel<TClass<int>, TClass<double>> a, int b2)
        {
            return null;
        }

       
        public System.Collections.ArrayList Hellow3()
        {
            return null;
        }

        [Category("3ϵ")]
        public async Task<Setting1> Test3()
        {
            return null;
        }

        public HttpResult http1()
        {
            return null;
        }
        public async Task<HttpResult> http2()
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
        public void DocumentReaderTest()
        {
            DocumentReader.GetTypeDocument(typeof(TestController));
        }

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
        public void TypeinfoBuilderTest()
        {
            var baseMethods = typeof(MicroServiceControllerBase).GetMethods().Select(m => m.Name).ToArray();
            var typeinfo = new ControllerTypeInfo()
            {
                Service = new JMS.Dtos.ServiceDetail { 
                Name = "testService",
                AllowGatewayProxy = true
                },
                Type = typeof(TestController),
                Methods = typeof(TestController).GetTypeInfo().DeclaredMethods.Where(m =>
                    m.IsStatic == false &&
                    m.IsPublic &&
                    m.IsSpecialName == false &&
                    m.DeclaringType != typeof(MicroServiceControllerBase) &&
                    baseMethods.Contains(m.Name) == false &&
                    m.DeclaringType != typeof(object)).OrderBy(m => m.Name).Select(m => new TypeMethodInfo(m , typeof(TestController))
                    {
                        NeedAuthorize = m.GetCustomAttribute<AuthorizeAttribute>() != null
                    }).ToArray()
            };
            var code = TypeInfoBuilder.Build(typeinfo).ToJsonString(true);
        }
    }
}