using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
namespace JMS.Common
{
    public class CommandArgParser : Way.Lib.Collections.IgnoreCaseDictionary
    {
        public CommandArgParser(string[] args)
        {
            foreach( var arg in args )
            {
                var arr = arg.Split(':');
                if(arr.Length > 1 )
                {
                    this[arr[0]] = arr[1];
                }
            }
        }
    }
}
