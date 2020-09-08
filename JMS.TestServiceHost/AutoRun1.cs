using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    class AutoRun1 : IScheduleTask
    {
        ILogger<AutoRun1> _logger;
        public double[] Timers => null;

        public int Interval => 60000*30;

        public AutoRun1(ILogger<AutoRun1> logger)
        {
            this._logger = logger;

        }

        public void Run()
        {
            _logger.LogInformation("AutoRun1执行 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
