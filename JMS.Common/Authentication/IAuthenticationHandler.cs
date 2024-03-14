﻿using System;
using System.Collections.Generic;
using System.Security.Claims;
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
        ClaimsPrincipal Authenticate(IDictionary<string, string> headers);

        /// <summary>
        /// 验证身份令牌
        /// </summary>
        /// <param name="token"></param>
        /// <returns>身份信息</returns>
        ClaimsPrincipal VerifyToken(string token);
    }

}
