using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class InvokeInfo
    {
        internal string ActionName;
        internal string ControllerFullName;
        internal string[] Parameters;
    }
}
