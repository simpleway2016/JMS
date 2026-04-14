using JMS.Dtos;
using JMS.InvokeConnects;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public void Wait()
        {
            if (_tasks.Count == 0)
            {
                return;
            }

            Task.WhenAll(_tasks.Select(m => m.Task)).Wait();

            _tasks.Clear();
        }

        /// <summary>
        /// 等待所有任务执行完毕
        /// </summary>
        /// <returns></returns>
        public async Task WaitAsync()
        {
            if (_tasks.Count == 0)
            {
                return;
            }

            await Task.WhenAll(_tasks.Select(m=>m.Task)).ConfigureAwait(false);

            _tasks.Clear();
        }
    }
}
