using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace JMS.Common.Json
{
    public static class ApplicationJsonSerializer
    {
        static IJsonSerializer _JsonSerializer = new DefaultJsonSerializer();
       /// <summary>
       /// json序列化工具类
       /// </summary>
        public static IJsonSerializer JsonSerializer
        {
            get
            {
                return _JsonSerializer;
            }
        }

       
    }
}
