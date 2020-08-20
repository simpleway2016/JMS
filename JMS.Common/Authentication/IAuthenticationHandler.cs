using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public interface IAuthenticationHandler
    {
        /// <summary>
        /// 验证身份，如果验证失败，将抛出异常
        /// </summary>
        /// <param name="headers"></param>
        /// <returns></returns>
        object Authenticate(IDictionary<string, string> headers);
    }
}
