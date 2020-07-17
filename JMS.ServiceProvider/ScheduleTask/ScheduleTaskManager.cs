using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using JMS.ScheduleTask;
using System.Linq;
using System.Threading;

namespace JMS
{
    class ScheduleTaskManager
    {
        MicroServiceHost _microServiceHost;
        ILogger<ScheduleTaskManager> _logger;
        List<ScheduleTaskController> _controllers = new List<ScheduleTaskController>();
        ManualResetEvent _waitObj = new ManualResetEvent(false);
        bool _started;
        public ScheduleTaskManager(MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
        }

        /// <summary>
        /// 启动所有任务
        /// </summary>
        public void StartTasks()
        {
            if (_started)
                return;

            
            _logger = _microServiceHost.ServiceProvider.GetService<ILogger<ScheduleTaskManager>>();
            _started = true;
            _waitObj.Set();
        }

        public void AddTask(IScheduleTask task)
        {
            var controller = _microServiceHost.ServiceProvider.GetService<ScheduleTask.ScheduleTaskController>();
            controller.Start(task,_waitObj);

            lock (_controllers)
            {
                _controllers.Add(controller);
            }
        }

        public void RemoveTask(IScheduleTask task)
        {
            ScheduleTaskController controller = null;
            try
            {
                for (int i = 0; i < _controllers.Count; i++)
                {
                    var ctrl = _controllers[i];
                    if (ctrl.Task == task)
                    {
                        controller = ctrl;
                        break;
                    }
                }
            }
            catch
            {

            }

            if(controller != null)
            {
                controller.Stop();
                lock (_controllers)
                {
                    _controllers.Remove(controller);
                }
            }

        }

        /// <summary>
        /// 终止所有任务
        /// </summary>
        public void StopTasks()
        {
            if (_started == false)
                return;

            _started = false;

            foreach ( var controller in _controllers )
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

            lock (_controllers)
            {
                _controllers.Clear();
            }           
        }
    }
}
