using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public interface IProcessExitHandler
    {
        /// <summary>
        /// 进程是否正在退出
        /// </summary>
        bool ProcessExited { get; }
        /// <summary>
        /// 添加一个在程序关闭时，需要执行的任务
        /// </summary>
        /// <param name="action"></param>
        void AddHandler(Action action);
        void RemoveHandler(Action action);
    }
    interface IProcessExitListener:IDisposable
    { 
        /// <summary>
        /// 进程是否正在退出
        /// </summary>
        bool ProcessExited { get; }
        void Listen(MicroServiceHost microServiceProvider);
    }
}
