using JMS.Infrastructures.Hardware;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace JMS.Infrastructures.Haredware
{
    class CpuInfoForWin : ICpuInfo
    {
        ILogger<CpuInfoForWin> _logger;
        PerformanceCounter _counter;
        public CpuInfoForWin(ILogger<CpuInfoForWin> logger)
        {
            this._logger = logger;
        }

        bool _hasError = false;
        public double GetCpuUsage()
        {
            if (_hasError)
                return 0;

            try
            {
                if (_counter == null)
                {
                    _counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                }
                return _counter.NextValue();
            }
            catch (Exception ex)
            {
                if (!_hasError)
                {
                    _hasError = true;
                    _logger?.LogError(ex, "获取cpu使用率错误");
                }
            }
            return 0;
        }
    }
}
