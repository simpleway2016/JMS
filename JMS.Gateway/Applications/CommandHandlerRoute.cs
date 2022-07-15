using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMS.Applications
{
    class CommandHandlerRoute:ICommandHandlerRoute
    {
        Dictionary<CommandType, ICommandHandler> _cache = new Dictionary<CommandType, ICommandHandler>();
        public CommandHandlerRoute(IServiceProvider serviceProvider)
        {
            var interfaceType = typeof(ICommandHandler);
            var handleTypes = typeof(CommandHandlerRoute).Assembly.DefinedTypes.Where(m => m.ImplementedInterfaces.Contains(interfaceType));
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
