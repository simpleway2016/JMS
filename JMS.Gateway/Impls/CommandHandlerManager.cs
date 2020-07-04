using JMS.Dtos;
using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMS.Impls
{
    class CommandHandlerManager:ICommandHandlerManager
    {
        Dictionary<CommandType, ICommandHandler> _cache = new Dictionary<CommandType, ICommandHandler>();
        public CommandHandlerManager(IServiceProvider serviceProvider)
        {
            var interfaceType = typeof(ICommandHandler);
            var handleTypes = typeof(CommandHandlerManager).Assembly.DefinedTypes.Where(m => m.ImplementedInterfaces.Contains(interfaceType));
            foreach( var type in handleTypes )
            {
                var handler = (ICommandHandler)Activator.CreateInstance(type,new object[] { serviceProvider });
                _cache[handler.MatchCommandType] = handler;
            }
        }

        /// <summary>
        /// 根据请求分配处理者
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public ICommandHandler AllocHandler(GatewayCommand cmd)
        {
            return _cache[cmd.Type];
        }
    }
}
