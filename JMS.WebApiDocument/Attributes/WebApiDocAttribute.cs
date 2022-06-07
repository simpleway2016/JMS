using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.WebApiDocument
{
    public class WebApiDocAttribute:Attribute
    {
        public Type MicroServiceType
        {
            get;
            init;
        }
        public string Description
        {
            get;
            init;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="microServiceType">映射的微服务客户端的类名称</param>
        /// <param name="desc">名称描述</param>
        public WebApiDocAttribute(Type microServiceType,string desc)
        {
            this.MicroServiceType = microServiceType;
            this.Description = desc;
        }
    }
}
