using JMS.Domains;
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
        List<Type> _taskBuffer = new List<Type>();
        public ScheduleTaskManager(MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
        }

        /// <summary>
        /// 启动所有任务
        /// </summary>
        internal void StartTasks()
        {
            if (_started)
                return;

            _logger = _microServiceHost.ServiceProvider.GetService<ILogger<ScheduleTaskManager>>();
            _started = true;

            foreach( var t in _taskBuffer)
            {
                runtask(t);
            }

            _waitObj.Set();
        }

        void runtask(Type taskType)
        {
            var controller = _microServiceHost.ServiceProvider.GetService<ScheduleTask.ScheduleTaskController>();
            controller.Start((IScheduleTask)_microServiceHost.ServiceProvider.GetService(taskType), _waitObj);

            lock (_controllers)
            {
                _controllers.Add(controller);
            }
        }

        public void AddTask(Type taskType)
        {
            if(!_started)
            {
                _taskBuffer.Add(taskType);
            }
            else
            {
                runtask(taskType);
            }            
        }

        public void RemoveTask(Type taskType)
        {
            if( !_started && _taskBuffer.Contains(taskType))
            {
                _taskBuffer.Remove(taskType);
                return;
            }
            try
            {
                for (int i = 0; i < _controllers.Count; i++)
                {
                    var ctrl = _controllers[i];
                    if (ctrl.Task.GetType() == taskType)
                    {
                        ctrl.Stop();
                        lock (_controllers)
                        {
                            _controllers.Remove(ctrl);
                        }
                    }
                }
            }
            catch
            {

            }

        }

        /// <summary>
        /// 终止所有任务
        /// </summary>
        internal void StopTasks()
        {
            if (_started == false)
                return;

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
            _taskBuffer.Clear();

            _started = false;
        }
    }
}
