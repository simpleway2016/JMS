using JMS.Infrastructures.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.IO;

namespace JMS.Infrastructures.Haredware
{
    class CpuInfoForLinux : ICpuInfo
    {
        ILogger<CpuInfoForLinux> _logger;
        public CpuInfoForLinux(ILogger<CpuInfoForLinux> logger)
        {
            _logger = logger;
        }

        bool _hasError = false;
        public double GetCpuUsage()
        {
            if (_hasError)
                return 0;

            try
            {
                using (FileStream file = new FileStream("/proc/stat", FileMode.Open, FileAccess.Read))
                using (StreamReader reader = new StreamReader(file, Encoding.UTF8))
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        throw new Exception("Error reading /proc/stat");
                    }

                    // Parse the CPU usage information from the first line of /proc/stat
                    string[] cpuInfo = line.Split(' ').Where(m => m.Length > 0).ToArray();
                    if (cpuInfo.Length < 9 || cpuInfo[0] != "cpu")
                    {
                        throw new Exception("Failed to parse /proc/stat");
                    }

                    // Parse user, nice, system, idle, iowait, irq, softirq, and steal times
                    ulong user = ulong.Parse(cpuInfo[1]);
                    ulong nice = ulong.Parse(cpuInfo[2]);
                    ulong system = ulong.Parse(cpuInfo[3]);
                    ulong idle = ulong.Parse(cpuInfo[4]);
                    ulong iowait = ulong.Parse(cpuInfo[5]);
                    ulong irq = ulong.Parse(cpuInfo[6]);
                    ulong softirq = ulong.Parse(cpuInfo[7]);
                    ulong steal = ulong.Parse(cpuInfo[8]);

                    // Calculate total and idle times
                    ulong total = user + nice + system + idle + iowait + irq + softirq + steal;
                    ulong idleTotal = idle + iowait;

                    // Calculate CPU usage as a percentage
                    double usage = ((double)(total - idleTotal) / total) * 100;

                    return usage;
                }
            }
            catch (Exception ex)
            {
                _hasError = true;
                _logger?.LogError(ex, "获取cpu使用率错误");
                return 0;
            }

            //try
            //{
            //    ProcessStartInfo info = new ProcessStartInfo("mpstat");
            //    info.UseShellExecute = false;
            //    info.RedirectStandardOutput = true;
            //    var process = Process.Start(info);

            //    process.WaitForExit();
            //    string output = process.StandardOutput.ReadToEnd();

            //    var lines = output.Split('\n').Where(m => m.Trim().Length > 0).Select(m => m.Trim()).ToArray();
            //    for (int i = 0; i < lines.Length; i++)
            //    {
            //        var line = lines[i];
            //        if (line.Contains(" CPU ") && line.Contains("%idle"))
            //        {
            //            line = lines[i + 1];
            //            string idle = Regex.Match(line, @"[0-9\.]+$").Value;

            //            return 100.0 - Convert.ToDouble(idle);
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    if (!_hasError)
            //    {
            //        _hasError = true;
            //        _logger?.LogError(ex, "获取cpu使用率错误");
            //    }
            //}
            return 0;
        }
    }
}
