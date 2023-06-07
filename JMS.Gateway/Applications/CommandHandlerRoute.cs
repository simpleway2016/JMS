using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace JMS.Applications
{
    class CommandHandlerRoute : ICommandHandlerRoute
    {
        IServiceProvider _serviceProvider;
        Dictionary<CommandType, ICommandHandler> _cache = new Dictionary<CommandType, ICommandHandler>();
        public CommandHandlerRoute(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;

        }

        public void Init()
        {
            var handlers = _serviceProvider.GetServices<ICommandHandler>();
            foreach( var handler in handlers)
            {
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
