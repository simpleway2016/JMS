using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Applications
{
    interface ICommandHandlerRoute
    {
        /// <summary>
        /// 根据请求分配处理者
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        ICommandHandler AllocHandler(GatewayCommand cmd);
        void Init();
    }
}
