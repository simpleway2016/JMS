using JMS;

namespace Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var gateways = new NetAddress[] {
                new NetAddress("127.0.0.1" , 8912)
            };

            long userId,moneyAccountId;
            using (var client = new RemoteClient(gateways))
            {
                //先确保那两个服务启动完毕
                IMicroService userService = null;
                while (userService == null) 
                {
                    await Task.Delay(1000);
                    userService = await client.TryGetMicroServiceAsync("UserInfoService");
                }
                IMicroService accountService = null;
                while (accountService == null)
                {
                    await Task.Delay(1000);
                    accountService = await client.TryGetMicroServiceAsync("MoneyAccountService");
                }

                //注册用户
                userId = await userService.InvokeAsync<long>("Register", "jack", "123");

                //开资金账户
                moneyAccountId = await accountService.InvokeAsync<long>("CreateAccount", userId);
            }


            using (var client = new RemoteClient(gateways))
            {
                //启动分布式事务
                client.BeginTransaction();
                IMicroService userService = await client.GetMicroServiceAsync("UserInfoService");
                IMicroService accountService = await client.GetMicroServiceAsync("MoneyAccountService");
                
                //修改密码
                userService.InvokeAsync("SetUserPassword", userId, "666");
                //增加余额
                accountService.InvokeAsync("AddMoney", moneyAccountId, 100);

                //再次增加余额
                var balance = await accountService.InvokeAsync<decimal>("AddMoney", moneyAccountId, 50);

                //提交分布式事务
                client.CommitTransaction();

                Console.WriteLine($"当前用户余额：{balance}");
            }


            using (var client = new RemoteClient(gateways))
            {
                IMicroService userService = await client.GetMicroServiceAsync("UserInfoService");
                var info = await userService.InvokeAsync<object>("GetUserInfo", userId);

                Console.WriteLine($"当前用户信息：{info}");
            }

            Console.ReadKey();
        }
    }
}