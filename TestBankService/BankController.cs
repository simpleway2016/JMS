using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using JMS;
using Microsoft.Extensions.Logging;

namespace TestBankService
{
    class BankController:MicroServiceControllerBase
    {
        ILogger<BankController> _logger;
        static Random Random = new Random();
        public BankController(ILogger<BankController> logger)
        {
            _logger = logger;
        }
        public long CreateBankAccount(TransactionDelegate tranDelegate,long userid , int seconds,double sucessRate)
        {
            for(int i = 0; i < seconds; i ++)
            {                
                _logger?.LogInformation("正在创建银行账号,{0}%", i * 10);
                Thread.Sleep(1000);
            }
            tranDelegate.CommitAction = () => {
                var val = Random.Next(1000);
                if (val / 1000.0 > sucessRate)
                    throw new Exception("提交失败");

                _logger.LogInformation("事务提交成功");
            };

            tranDelegate.RollbackAction = () => _logger.LogInformation("事务回滚了");
            return userid;
        }
    }
}
