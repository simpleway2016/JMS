using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    /// <summary>
    /// 根据微服务设定的检查代码，检查客户端是否可以连接此微服务
    /// </summary>
    public interface IClientCheck
    {
        AssemblyCSharpBuilder CodeBuilder { get; set; }
        string ClientCode { get; set; }
        bool Check(IDictionary<string,string> headers);
    }
}
