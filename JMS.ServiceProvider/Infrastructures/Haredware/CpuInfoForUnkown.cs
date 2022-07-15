using JMS.Infrastructures.Hardware;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Infrastructures.Haredware
{
    class CpuInfoForUnkown : ICpuInfo
    {
        public double GetCpuUsage()
        {
            return 0;
        }
    }
}
