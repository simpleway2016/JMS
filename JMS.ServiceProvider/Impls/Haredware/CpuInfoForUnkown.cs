using JMS.Interfaces.Hardware;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Impls.Haredware
{
    class CpuInfoForUnkown : ICpuInfo
    {
        public double GetCpuUsage()
        {
            return 0;
        }
    }
}
