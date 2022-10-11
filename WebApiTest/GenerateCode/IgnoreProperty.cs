using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace WebApiTest.GenerateCode
{
    public static class IgnoreProperty
    {
        public static IgnoreItem[] IngoreItems
        {
            get
            {
                return new IgnoreItem[0];
            }
        }
        
    }

    public class IgnoreItem
    {
        public Type ClassType { get; set; }
        public string[] IgnoreNames { get; set; }
    }
}
