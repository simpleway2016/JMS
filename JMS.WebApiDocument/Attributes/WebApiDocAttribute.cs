﻿using System;
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
            private set;
        }
        public string Description
        {
            get;
            private set;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="microServiceType">映射的微服务客户端的类名称，如果是给普通controller设置文档，这个参数应该是null</param>
        /// <param name="desc">名称描述</param>
        public WebApiDocAttribute(Type microServiceType,string desc)
        {
            this.MicroServiceType = microServiceType;
            this.Description = desc;
        }

       /// <summary>
       /// 
       /// </summary>
       /// <param name="desc">名称描述</param>
        public WebApiDocAttribute(string desc)
        {
            this.Description = desc;
        }
    }
}
