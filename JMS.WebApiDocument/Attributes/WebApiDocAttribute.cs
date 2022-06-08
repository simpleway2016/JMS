using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.WebApiDocument
{
    /// <summary>
    /// 标识这个方法只是用来显示某种说明，不需要在文档中展示这个方法的url，request等信息
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class IsCommentAttribute : Attribute
    {
      
    }
}
