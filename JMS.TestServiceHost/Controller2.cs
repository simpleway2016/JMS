using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    class TestObject
    {
        public string Name;
    }
    class Controller2 : MicroServiceControllerBase
    {
        public Controller2(ILogger<Controller2> logger)
        {
            this.TransactionControl = new TransactionDelegate(this.TransactionId) { 
                CommitAction = () => {
                    this.TryUnLock("testkey");
                    logger.LogInformation("unlocked testkey");
                    logger.LogInformation("Controller2 提交事务");
                },
                RollbackAction = () => {
                    logger.LogInformation("Controller2 回滚事务");
                }
            };
        }

        /// <summary>
        /// 获取名称
        /// </summary>
        /// <returns></returns>
        public string GetName(TestObject t)
        {
            return "lock result: " + this.TryLock("testkey") + " name:" + t.Name;
        }
    }
}
