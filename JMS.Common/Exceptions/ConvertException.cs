using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class ConvertException : Exception
    {
        public string Source { get; }
        public ConvertException( string source, string msg):base(msg)
        {
            this.Source = source;
        }
    }
}
