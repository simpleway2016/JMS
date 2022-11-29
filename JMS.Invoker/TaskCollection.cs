using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    internal class TaskCollection
    {
        List<Task> _tasks = new List<Task>();
        public void AddTask(Task task)
        {
            lock (_tasks)
            {
                _tasks.Add(task);
            }
        }

        /// <summary>
        /// 等待所有任务执行完毕
        /// </summary>
        /// <returns></returns>
        public List<Exception> Wait()
        {
            if (_tasks.Count == 0)
            {
                return null;
            }

            List<Exception> ret = new List<Exception>(_tasks.Count);
            for (int i = 0; i < _tasks.Count; i++)
            {
                try
                {
                    _tasks[i].Wait();
                }
                catch (Exception ex)
                {
                    ret.Add(ex);
                }
            }
            
            _tasks.Clear();
            return ret;
        }

        /// <summary>
        /// 等待所有任务执行完毕
        /// </summary>
        /// <returns></returns>
        public async Task<List<Exception>> WaitAsync()
        {
            if (_tasks.Count == 0)
            {
                return null;
            }

            List<Exception> ret = new List<Exception>(_tasks.Count);
            for (int i = 0; i < _tasks.Count; i++)
            {
                try
                {
                    await _tasks[i];
                }
                catch (Exception ex)
                {
                    ret.Add(ex);
                }
            }

            _tasks.Clear();
            return ret;
        }
    }
}
