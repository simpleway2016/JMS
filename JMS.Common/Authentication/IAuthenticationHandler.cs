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
        TokenContent Authenticate(IDictionary<string, string> headers);

        /// <summary>
        /// 验证身份令牌
        /// </summary>
        /// <param name="token"></param>
        /// <returns>身份信息</returns>
        TokenContent VerifyToken(string token);
    }

    public class TokenContent
    {
        public object Content { get; set; }
        public string Role { get; set; }
    }
}
