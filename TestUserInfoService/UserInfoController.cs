using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using JMS;
using Microsoft.Extensions.Logging;

namespace TestUserInfoService
{
    class UserInfoController:MicroServiceControllerBase
    {
        ILogger<UserInfoController> _logger;
        static Random Random = new Random();
        public UserInfoController(ILogger<UserInfoController> logger)
        {
            _logger = logger;
        }
        public long CreateUser(TransactionDelegate tranDelegate,int seconds,double sucessRate)
        {
            for(int i = 0; i < seconds; i ++)
            {                
                _logger?.LogInformation("正在创建用户,{0}%", i * 10);
                Thread.Sleep(1000);
            }
            tranDelegate.CommitAction = () => {
                var val = Random.Next(1000);
                if (val / 1000.0 > sucessRate)
                    throw new Exception("提交失败");

                _logger.LogInformation("事务提交成功");
            };

            tranDelegate.RollbackAction = () => _logger.LogInformation("事务回滚了");
            return DateTime.Now.Ticks;
        }
    }
}
