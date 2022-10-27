using JMS.Infrastructures.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace JMS.Infrastructures.Haredware
{
    class CpuInfoForWin : ICpuInfo
    {
        PerformanceCounter _counter;
        public CpuInfoForWin()
        {
        }
        public double GetCpuUsage()
        {
            if(_counter == null)
            {
                _counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            return _counter.NextValue();
        }
    }
}
