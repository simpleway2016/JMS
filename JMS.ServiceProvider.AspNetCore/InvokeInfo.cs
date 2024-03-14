using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class InvokeInfo
    {
        public string ActionName { get; set; }
        public string ControllerFullName { get; set; }
        public string[] Parameters { get; set; }
    }
}
