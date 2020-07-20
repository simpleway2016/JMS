using JMS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<MicroServiceTransaction>>();

            ///肯定成功的测试
            //try
            //{
            //    using (var tran = new MicroServiceTransaction("192.168.40.131", 7900, null, logger))
            //    {
            //        var userInfoService = tran.GetMicroService("UserInfo");
            //        userInfoService.InvokeAsync("CreateUser", 10, 1);

            //        var bankService = tran.GetMicroService("Bank");
            //        bankService.InvokeAsync("CreateBankAccount", 2, 10, 1);

            //        logger.LogInformation("准备提交事务");
            //        tran.Commit();
            //        logger.LogInformation("成功提交事务");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    logger.LogError(ex, ex.Message);
            //}

            ///肯定出错的测试（异步）
            //try
            //{
            //    using (var tran = new MicroServiceTransaction("192.168.40.131", 7900, null, logger))
            //    {
            //        var userInfoService = tran.GetMicroService("UserInfo");
            //        userInfoService.InvokeAsync("CreateUser", 3, 1);

            //        var bankService = tran.GetMicroService("Bank");
            //        bankService.InvokeAsync("CreateBankAccount", 0, 3, 1);

            //        logger.LogInformation("准备提交事务");
            //        tran.Commit();
            //        logger.LogInformation("成功提交事务");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    logger.LogError(ex, ex.Message);
            //}


            ///肯定出错的测试（同步）
            //try
            //{
            //    using (var tran = new MicroServiceTransaction("192.168.40.131", 7900, null, logger))
            //    {
            //        var userInfoService = tran.GetMicroService("UserInfo");
            //        userInfoService.Invoke("CreateUser", 0, 1);

            //        var bankService = tran.GetMicroService("Bank");
            //        bankService.Invoke("CreateBankAccount", 0, 10, 1);

            //        logger.LogInformation("准备提交事务");
            //        tran.Commit();
            //        logger.LogInformation("成功提交事务");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    logger.LogError(ex, ex.Message);
            //}


            ///创建银行账户，提交事务时，会失败，主要测试日志是否详细记录下请求信息
            //try
            //{
            //    using (var tran = new MicroServiceTransaction("192.168.40.131", 7900, null, logger))
            //    {
            //        var userInfoService = tran.GetMicroService("UserInfo");
            //        userInfoService.InvokeAsync("CreateUser", 1, 1);

            //        var bankService = tran.GetMicroService("Bank");
            //        bankService.InvokeAsync("CreateBankAccount", 2, 1, 0);

            //        logger.LogInformation("准备提交事务");
            //        tran.Commit();
            //        logger.LogInformation("成功提交事务");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    logger.LogError(ex,"提交事务失败");
            //}


            ///中断进程，让微服务自己回滚事务
            //try
            //{
            //    using (var tran = new MicroServiceTransaction("192.168.40.131", 7900, null, logger))
            //    {
            //        var userInfoService = tran.GetMicroService("UserInfo");
            //        userInfoService.InvokeAsync("CreateUser", 1, 1);

            //        var bankService = tran.GetMicroService("Bank");
            //        bankService.InvokeAsync("CreateBankAccount", 2, 1, 1);

            //        System.Diagnostics.Process.GetCurrentProcess().Kill();
            //    }
            //}
            //catch (Exception ex)
            //{
            //    logger.LogError(ex, "提交事务失败");
            //}


            Thread.Sleep(2000000);
        }
    }
}
