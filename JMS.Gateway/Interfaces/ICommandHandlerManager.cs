using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface ICommandHandlerManager
    {
        /// <summary>
        /// 根据请求分配处理者
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        ICommandHandler AllocHandler(GatewayCommand cmd);
    }
}
