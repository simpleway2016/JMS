using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.ScheduleTask
{
    /// <summary>
    /// 安全的Task工厂，当进程使用kill -15 命令退出时，该工厂创建的task必须执行完毕，才允许进程退出
    /// </summary>
    public class SafeTaskFactory : TaskFactory
    {
        public SafeTaskFactory():base(new SafeTaskScheduler())
        {

        }

        public void WaitAll(int millisecondsTimeout = -1)
        {
            ((SafeTaskScheduler)this.Scheduler).WaitAll(millisecondsTimeout);
        }

    }

    class SafeTaskScheduler : TaskScheduler
    {
        LinkedList<Task> _tasks = new LinkedList<Task>();
        public SafeTaskScheduler()
        {

        }


        /// <summary>Queues a task to the scheduler.</summary> 
        /// <param name="task">The task to be queued.</param> 
        protected sealed override void QueueTask(Task task)
        {
            lock (_tasks)
            {
                _tasks.AddLast(task);
                NotifyThreadPoolOfPendingWork(task);
            }
        }

        /// <summary> 
        /// Informs the ThreadPool that there's work to be executed for this scheduler. 
        /// </summary> 
        private void NotifyThreadPoolOfPendingWork(Task task)
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                base.TryExecuteTask(task);
                lock(_tasks)
                {
                    _tasks.Remove(task);
                }
            }, null);
        }

        /// <summary>Attempts to execute the specified task on the current thread.</summary> 
        /// <param name="task">The task to be executed.</param> 
        /// <param name="taskWasPreviouslyQueued"></param> 
        /// <returns>Whether the task could be executed on the current thread.</returns> 
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            var ret = base.TryExecuteTask(task);
            if (_tasks.Contains(task))
            {
                lock (_tasks)
                {
                    _tasks.Remove(task);
                }
            }
            return ret;
        }

        /// <summary>Attempts to remove a previously scheduled task from the scheduler.</summary> 
        /// <param name="task">The task to be removed.</param> 
        /// <returns>Whether the task could be found and removed.</returns> 
        protected sealed override bool TryDequeue(Task task)
        {
            if (_tasks.Contains(task))
            {
                lock (_tasks)
                {
                    _tasks.Remove(task);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

    

        /// <summary>Gets an enumerable of the tasks currently scheduled on this scheduler.</summary> 
        /// <returns>An enumerable of the tasks currently scheduled.</returns> 
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_tasks)
            {
                return _tasks.ToArray();
            }
        }

        public void WaitAll(int millisecondsTimeout = -1)
        {
            var tasks = (Task[])this.GetScheduledTasks();
            Task.WaitAll(tasks, millisecondsTimeout);
        }
    }
}
