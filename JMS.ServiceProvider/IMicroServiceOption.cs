using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public interface IMicroServiceOption
    {
        /// <summary>
        /// 微服务描述
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// 最多允许多少个请求数。默认值为0，表示无限制。
        /// </summary>
        int MaxRequestCount { get; set; }

        /// <summary>
        /// 自定义客户端检验代码
        /// </summary>
        string ClientCheckCode { get; set; }

        /// <summary>
        /// 当与网关连接断开时，是否自动关闭进程
        /// 在双机热备的情况下，可以考虑设置此属性为true，因为与网关连接断开后，可能继续运行业务，会与后面启动的备份服务产生冲突
        /// </summary>
        bool AutoExitProcess
        {
            get;
            set;
        }

        /// <summary>
        /// 是否同一时间只有一个相同的服务器运行（双机热备）
        /// 当此属性设为true，如果与网关连接断开，会自动退出进程
        /// </summary>
        bool SingletonService
        {
            get;
            set;
        }
    }
}
