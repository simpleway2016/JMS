using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public interface IClientCheck
    {
        AssemblyCSharpBuilder CodeBuilder { get; set; }
        string ClientCode { get; set; }
        bool Check(string arg);
    }
}
