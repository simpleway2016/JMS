using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.ScheduleTask
{
    enum TaskState
    {
        Stopped = 0,
        Running = 1,
        WaitToStop = 2,
        WaitToRun = 3
    }
    class ScheduleTaskController
    {
        public IScheduleTask Task { get; private set; }
        ILogger<ScheduleTaskController> _logger;
        Thread _thread;
        TaskState _state = TaskState.Stopped;
        ManualResetEvent _waitobject = new ManualResetEvent(false);
        DateTime _lastRunTime = DateTime.Now;
        ManualResetEvent _mainWaitObj;
        public ScheduleTaskController(ILogger<ScheduleTaskController> logger)
        {
            _logger = logger;
        }

        public void Start(IScheduleTask task,ManualResetEvent waitObj)
        {
            _state = TaskState.WaitToRun;
            _mainWaitObj = waitObj;           
            this.Task = task;
            _thread = new Thread(run);
            _thread.Start();
        }

        void run()
        {
            _mainWaitObj.WaitOne();
            if(_state == TaskState.Stopped)
            {
                return;
            }

            _state = TaskState.Running;
            string taskname = this.Task.GetType().FullName;
            while (_state == TaskState.Running)
            {
                int sleepTime = 10;
                try
                {
                    bool toRun = false;
                    if (this.Task.Timers != null && this.Task.Timers.Length > 0)
                    {
                        //如果是定点执行，可以让Thread.Sleep睡眠长一点
                        sleepTime = 60000;
                    }
                    else if(this.Task.Interval <= 10000)
                    {
                        sleepTime = this.Task.Interval;
                        toRun = true;
                    }
                    else
                    {
                        sleepTime = Math.Min(60000, this.Task.Interval / 2);
                    }


                    if (!toRun)
                    {
                        if (this.Task.Timers != null && this.Task.Timers.Length > 0)
                        {
                            //每天特定时间执行
                            foreach (double hour in this.Task.Timers)
                            {
                                int h = (int)hour;//过滤出哪个小时
                                int m = (int)((hour % 1) * 100);//过滤出分钟

                                //转换成当天的执行时间点
                                DateTime time = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd " + h + ":" + m + ":00"));
                                if (DateTime.Now >= time && _lastRunTime < time)
                                {
                                    _logger?.LogInformation("执行任务：{0}", taskname);
                                    toRun = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            //间隔多少豪秒执行一次
                            var milliseconds = int.MaxValue;
                            if (_lastRunTime != null)
                            {
                                milliseconds = (int)(DateTime.Now - _lastRunTime).TotalMilliseconds;
                            }
                            if (milliseconds >= this.Task.Interval)
                            {
                                toRun = true;
                            }
                            else
                            {
                                //例如，如果6000执行一次，现在已经过了5000毫秒了，那么再sleep(1000)就可以了
                                milliseconds = this.Task.Interval - milliseconds;
                                if (milliseconds > 0 && milliseconds < sleepTime)
                                {
                                    Thread.Sleep((int)milliseconds);
                                    toRun = true;
                                }
                            }
                        }
                    }

                    if (toRun)
                    {
                        _lastRunTime = DateTime.Now;
                        this.Task.Run();
                    }                   
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "执行任务{0}出错", Task.GetType().FullName);
                }
                _waitobject.WaitOne(sleepTime);
                _waitobject.Reset();
            }
            _state = TaskState.Stopped;
        }

        public void Stop()
        {
            if (_state == TaskState.WaitToRun || _state == TaskState.Stopped)
            {
                _state = TaskState.Stopped;
                return;
            }

            _logger?.LogInformation("准备停止任务{0}", Task.GetType().FullName);
            _state = TaskState.WaitToStop;
            _waitobject.Set();
            while (_state == TaskState.WaitToStop)
                Thread.Sleep(10);
        }
    }
}
