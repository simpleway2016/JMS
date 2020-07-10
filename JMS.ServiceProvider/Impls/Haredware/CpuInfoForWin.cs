using JMS.Interfaces.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace JMS.Impls.Haredware
{
    class CpuInfoForWin : ICpuInfo
    {
        PerformanceCounter _counter;
        public CpuInfoForWin()
        {
            _counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _counter.NextValue();
        }
        public double GetCpuUsage()
        {
            return _counter.NextValue();
        }
    }
}
