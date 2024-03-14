using JMS.Dtos;
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
        ICommandHandler[] _cache;
        public CommandHandlerRoute(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;

        }

        public void Init()
        {
            var handlers = _serviceProvider.GetServices<ICommandHandler>();
            _cache = new ICommandHandler[handlers.Max(m=>(int)m.MatchCommandType) + 1];
            foreach ( var handler in handlers)
            {
                _cache[(int)handler.MatchCommandType] = handler;
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
