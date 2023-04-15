using System;
using System.Collections.Generic;
using System.Text;
using static MicroServiceControllerBase;
using System.Threading;

namespace JMS
{
    public class BaseJmsController
    {
        internal static AsyncLocal<LocalObject> RequestingObject = new AsyncLocal<LocalObject>();

        private IDictionary<string, string> _Headers;
        /// <summary>
        /// 请求的头
        /// </summary>
        public IDictionary<string, string> Headers
        {
            get
            {
                if (_Headers == null && RequestingObject.Value != null)
                {
                    _Headers = RequestingObject.Value.Command.Header;
                }
                return _Headers;
            }
        }

        object _userContent;
        /// <summary>
        /// 身份验证后获取的身份信息
        /// </summary>
        public object UserContent
        {
            get
            {
                if (_userContent == null && RequestingObject.Value != null)
                {
                    _userContent = RequestingObject.Value.UserContent;
                }
                return _userContent;
            }
        }

        IServiceProvider _ServiceProvider;
        /// <summary>
        /// Controller的依赖注入服务提供者
        /// </summary>
        public IServiceProvider ServiceProvider
        {
            get
            {
                if (_ServiceProvider == null && RequestingObject.Value != null)
                {
                    _ServiceProvider = RequestingObject.Value.ServiceProvider;
                }
                return _ServiceProvider;
            }
        }
    }
}
