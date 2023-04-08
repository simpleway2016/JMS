using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Common
{
    public class ConfigurationValue<T>
    {
        /// <summary>
        /// 转换后对象，对象会自动随着配置文件更新而更新
        /// </summary>
        public T Current { get; private set; }
        IDisposable _disposable;
        internal ConfigurationValue(IConfiguration configuration)
        {
            callback(configuration);
        }

        void callback(object state)
        {
            _disposable?.Dispose();
            IConfiguration configuration = (IConfiguration)state;
            this.Current = configuration.Get<T>();
            _disposable = configuration.GetReloadToken().RegisterChangeCallback(callback, configuration);
        }
    }
}
