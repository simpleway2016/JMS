using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using JMS.ScheduleTask;
using System.Linq;

namespace JMS
{
    class ScheduleTaskManager
    {
        MicroServiceHost _microServiceHost;
        List<IScheduleTask> _tasks = new List<IScheduleTask>();
        ILogger<ScheduleTaskManager> _logger;
        List<ScheduleTaskController> _controllers = new List<ScheduleTaskController>();
        public ScheduleTaskManager(MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
        }

        /// <summary>
        /// 启动所有任务
        /// </summary>
        public void StartTasks()
        {
            _logger = _microServiceHost.ServiceProvider.GetService<ILogger<ScheduleTaskManager>>();
            foreach ( var task in _tasks )
            {
                var controller = _microServiceHost.ServiceProvider.GetService<ScheduleTask.ScheduleTaskController>();
                controller.Start(task);
                _controllers.Add(controller);
            }
        }

        public void AddTask(IScheduleTask task)
        {
            _tasks.Add(task);
        }

        public void RemoveTask(IScheduleTask task)
        {
            var controller = _controllers.FirstOrDefault(m=>m.Task == task);
            if(controller != null)
            {
                controller.Stop();
                _controllers.Remove(controller);
            }
            _tasks.Remove(task);
        }

        /// <summary>
        /// 终止所有任务
        /// </summary>
        public void StopTasks()
        {
            foreach( var controller in _controllers )
            {
                try
                {
                    controller.Stop();
                }
                catch(Exception ex)
                {
                    _logger?.LogError(ex, "停止任务{0}出错", controller.Task.GetType().FullName);
                }
            }
            _controllers.Clear();
            _tasks.Clear();
        }
    }
}
