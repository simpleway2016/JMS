using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces
{
    interface ICodeBuilder
    {
        string GenerateCode(string nameSpace,string className,string serviceName);
    }
}
