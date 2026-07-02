using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.MicroServiceHost.Controllers
{
    public class DemoController : BaseController
    {
        /// <summary>
        /// 一个支持分布式事务的方法
        /// </summary>
        /// <returns></returns>
        public int Method1()
        {
            //只需要启动事务即可，后面不需要自己提交事务，微服务的客户端会帮你调用提交事务
            //BaseController的OnAfterAction实现了事务的托管
            this.CurrentDBContext.BeginTransaction();

            //...这里写操作数据库代码

            return 0;
        }
    }
}
