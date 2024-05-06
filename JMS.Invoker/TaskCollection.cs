using JMS.Dtos;
using JMS.InvokeConnects;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    class ConnectTask
    {
        public ConnectTask(IInvokeConnect invokeConnect ,int invokingId, Task task)
        {
            InvokeConnect = invokeConnect;
            InvokingId = invokingId;
            Task = task;
        }

        public IInvokeConnect InvokeConnect { get; }
        public int InvokingId { get; }
        public Task Task { get; }
    }
    internal class TaskCollection
    {
        List<ConnectTask> _tasks = new List<ConnectTask>();
        public int Count => _tasks.Count;
        public void AddTask( IInvokeConnect invokeConnect ,int invokingId, Task task)
        {
            lock (_tasks)
            {
                _tasks.Add(new ConnectTask(invokeConnect ,invokingId, task));
            }
        }

        public async Task WaitConnectComplete(int invokingId,IInvokeConnect invokeConnect)
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                var item = _tasks[i];
                if (item.InvokeConnect == invokeConnect)
                {
                    //只等待那些InvokingId 比我小的任务，
                    //否则，我如果等待比我大的，那么那些比我大的也会等待我，互相等待就卡死了
                    if (item.InvokingId >= invokingId)
                        continue;

                    await item.Task;
                }
            }
        }

        public void Clear()
        {
            _tasks.Clear();
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
                    _tasks[i].Task.Wait();
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
                    await _tasks[i].Task;
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
