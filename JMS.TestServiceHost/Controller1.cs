using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class Controller1: MicroServiceControllerBase
    {
        ILogger<Controller1> _logger;
        public Controller1(ILogger<Controller1> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// test测试
        /// </summary>
        /// <returns></returns>
        public string test()
        {
            return "abc";
        }
        public void Test2(TransactionDelegate tran)
        {
            tran.CommitAction = () => {
                _logger.LogInformation("Controller1 Test2 提交事务");
            };
            tran.RollbackAction = () => {
                _logger.LogInformation("Controller1 Test2 回滚事务");
            };
            _logger.LogInformation("Controller1.Test2 收到头部 auth:{0}", this.Header["auth"]);
        }

        /// <summary>
        /// 测试
        /// </summary>
        /// <param name="tran"></param>
        /// <param name="p">年龄</param>
        /// <param name="str">名称</param>
        /// <returns>返回需要的字符串</returns>
        public string Test( TransactionDelegate tran, int? p,string str)
        {
            tran.CommitAction = () => {
                _logger.LogInformation("Controller1 Test 提交事务");
            };
            tran.RollbackAction = () => {
                _logger.LogInformation("Controller1 Test 回滚事务");
            };
            _logger.LogInformation("Controller1 收到头部 auth:{0}" , this.Header["auth"]);
            return "abc" + p + " " + str;
        }

        public long IntTest()
        {
            return DateTime.Now.Ticks;
        }

        public string ErrTest()
        {
            throw new Exception("ErrTest异常啦");
        }
    }
}
