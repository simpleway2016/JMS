using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.GenerateCode
{
    interface ICodeBuilder
    {
        string GenerateCode(string nameSpace,string className,string serviceName);
    }
}
