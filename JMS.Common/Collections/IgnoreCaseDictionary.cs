using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Common.Collections
{
    /// <summary>
    /// 忽略大小写的Dictionary
    /// </summary>
    public class IgnoreCaseDictionary : Dictionary<string, string>
    {
        public IgnoreCaseDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {

        }



    }
}