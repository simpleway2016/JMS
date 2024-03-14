using JMS;

namespace WebApiTest.TestMicroService
{
    public class TestHost
    {
        public static void Start()
        {
            ServiceCollection services = new ServiceCollection();

            var gateways = new NetAddress[] {
                   new NetAddress{
                        Address = "localhost",
                        Port = 8911
                   }
                };

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });

            var msp = new MicroServiceHost(services);
            msp.Register<TestController>("TestService");
            msp.ServiceProviderBuilded += Msp_ServiceProviderBuilded;
            msp.Build(9000, gateways)
                .Run();
        }

        private static void Msp_ServiceProviderBuilded(object? sender, IServiceProvider e)
        {
           
        }
    }

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

    /// <summary>
    /// 测试controller
    /// </summary>
    class TestController : MicroServiceControllerBase
    {
        public TestModel<TClass<int>, TClass<string>> Hellow()
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

        private static readonly string[] Summaries = new[]
      {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
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
        /// <summary>
        /// 无返回值的方法
        /// </summary>
        /// <param name="age">年龄</param>
        public void ReturnEmpty(int? age)
        {

        }
        public Setting1 Test3()
        {
            return null;
        }
        public TestEnum Testenum()
        {
            return TestEnum.Normal;
        }

        public override void OnAfterAction(string actionName, object[] parameters)
        {
            base.OnAfterAction(actionName, parameters);
        }
    }

    /// <summary>
    /// 测试枚举
    /// </summary>
    public enum TestEnum
    {
        /// <summary>
        /// 正常的
        /// </summary>
        Normal = 1,
        /// <summary>
        /// 其他的
        /// </summary>
        Other = 2,
    }
}
