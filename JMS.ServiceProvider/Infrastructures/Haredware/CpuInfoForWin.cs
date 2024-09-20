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
        CpuMonitor _cpuMonitor;
        public CpuInfoForWin(ILogger<CpuInfoForWin> logger)
        {
            this._logger = logger;
           
        }

        bool _hasError = false;
        public double GetCpuUsage()
        {
            if (_hasError)
                return 0;

            if(_cpuMonitor == null)
                _cpuMonitor = new CpuMonitor();

            try
            {
                return _cpuMonitor.GetCpuUsage();
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


        public class CpuMonitor
        {
            private PerformanceCounter _cpuCounter;

            public CpuMonitor()
            {
                // 初始化PerformanceCounter（如果可用的话）
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                }
                catch (Exception)
                {
                    // 如果PerformanceCounter不可用，则忽略初始化
                }
            }

            public double GetCpuUsage()
            {
                if (_cpuCounter != null)
                {
                    // 如果PerformanceCounter可用，则使用它来获取CPU使用率
                    return _cpuCounter.NextValue();
                }
                else
                {
                    // 使用备用方法获取CPU使用率
                    return GetCpuUsageAlternative();
                }
            }

            private double GetCpuUsageAlternative()
            {
                var cpuTime1 = Process.GetCurrentProcess().TotalProcessorTime;
                System.Threading.Thread.Sleep(1000); // 等待1秒
                var cpuTime2 = Process.GetCurrentProcess().TotalProcessorTime;

                var cpuUsedMs = (cpuTime2 - cpuTime1).TotalMilliseconds; // 计算差值

                var totalMs = 1000; // 我们等待了1秒
                var cpuUsage = (cpuUsedMs / (Environment.ProcessorCount * totalMs)) * 100; // 计算CPU使用率

                return cpuUsage;
            }
        }
    }
}
