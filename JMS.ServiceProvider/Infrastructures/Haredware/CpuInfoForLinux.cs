using JMS.Infrastructures.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace JMS.Infrastructures.Haredware
{
    class CpuInfoForLinux : ICpuInfo
    {
        ILogger<CpuInfoForLinux> _logger;
        public CpuInfoForLinux(ILogger<CpuInfoForLinux> logger)
        {
            _logger = logger;
        }
        public double GetCpuUsage()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("mpstat", "-P ALL");
                info.UseShellExecute = false;
                info.RedirectStandardOutput = true;
                var process = Process.Start(info);

                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();

                var lines = output.Split('\n').Where(m => m.Trim().Length > 0).Select(m => m.Trim()).ToArray();
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (line.Contains(" AM ") && line.Contains(" CPU "))
                    {
                        line = lines[i + 1];
                        string idle = Regex.Match(line, @"[0-9\.]+$").Value;

                        return 100.0 - Convert.ToDouble(idle);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取cpu使用率错误");
            }
            return 0;
        }
    }
}
