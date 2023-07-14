using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    /// <summary>
    /// 标识Controller或者函数需要进行身份验证
    /// </summary>
    public class AuthorizeAttribute: Attribute
    {
        /// <summary>
        /// 获取或设置允许访问资源的角色的逗号分隔列表。
        /// </summary>
        public string Roles { get; set; }
    }
}
