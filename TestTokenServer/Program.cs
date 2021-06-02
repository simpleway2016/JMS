using System;

namespace TestTokenClient1
{
    class Program
    {
        static void Main(string[] args)
        {
            JMS.Token.TokenClient client = new JMS.Token.TokenClient("127.0.0.1", 9911);

            var token = client.BuildLongWithExpire(123456, DateTime.Now.AddMinutes(10));
            Console.WriteLine($"创建long token:{token}");

            var longValue = client.VerifyLong(token);
            Console.WriteLine($"验证token结果为:{longValue}");


            token = client.BuildStringWithExpire("my info", DateTime.Now.AddMinutes(10));
            Console.WriteLine($"创建string token:{token}");

            var stringValue = client.VerifyString(token);
            Console.WriteLine($"验证token结果为:{stringValue}");

            client.SetTokenDisable(token, DateTime.Now.AddMinutes(10).ToUniversalTime());
            Console.WriteLine($"设置token失效");

            try
            {
                Console.WriteLine($"再次验证token:{token}");
                stringValue = client.VerifyString(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"验证token失败，" + ex.Message);
            }
            Console.ReadKey();
        }
    }
}
