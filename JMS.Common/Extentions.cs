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
        
    }
}
