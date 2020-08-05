using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    class TestObject
    {
        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 子类型
        /// </summary>
        public TestObject Child { get; set; }

        /// <summary>
        /// 年龄
        /// </summary>
        public int Age;
    }
    class TestObject2
    {
        /// <summary>
        /// 年龄2
        /// </summary>
        public int Age2;
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

        /// <summary>
        /// 获取名字2
        /// </summary>
        /// <param name="t"></param>
        /// <param name="t2"></param>
        /// <returns></returns>
        public string GetName2(TestObject[] t, List<TestObject2> t2)
        {
            return null;
        }

        public TestObject2 GetObject2()
        {
            return null;
        }
    }
}
