using JMS.Common;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS
{
    public static class Extentions
    {
        public static string[] GetStringArrayParameters(this object[] source)
        {
            if (source == null)
                return null;

            string[] ret = new string[source.Length];
            for(int i = 0; i < source.Length; i ++)
            {
                ret[i] = source[i].ToJsonString();
            }
            return ret;
        }

        /// <summary>
        /// 和Get方法类似，此方法返回的对象，会自动随着配置文件内容变更而更新
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static ConfigurationValue<T> GetNewest<T>(this IConfiguration configuration)
        {
            var obj = new ConfigurationValue<T>(configuration);
            return obj;
        }
        
    }
}
