using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.WebApiDocument
{
    /// <summary>
    /// 在文档界面添加一个按钮
    /// </summary>
    [AttributeUsage(AttributeTargets.Class,AllowMultiple =true)]
    public class WebApiDocButtonAttribute : Attribute
    {
        public string Name { get; }
        public string Url { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="url"></param>
        public WebApiDocButtonAttribute(string name, string url)
        {
            this.Name = name;
            this.Url = url;
        }
    }
}
