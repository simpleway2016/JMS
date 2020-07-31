using JMS.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    /// <summary>
    /// 关于进程退出时的处理,linux如果要关闭进程，需要使用 "kill -15 进程id" 命令，这样，ProcessExitHandler才能处理
    /// </summary>
    class ProcessExitHandler :IProcessExitHandler,IProcessExitListener
    {
        public MicroServiceHost _microServiceHost;
        TransactionDelegateCenter _transactionDelegateCenter;
        bool _ProcessExited = false;
        public bool ProcessExited => _ProcessExited;

        ILogger<ProcessExitHandler> _logger;
        ScheduleTaskManager _scheduleTaskManager;
        List<Action> _missions = new List<Action>();
        public ProcessExitHandler(
            TransactionDelegateCenter transactionDelegateCenter,
            ScheduleTaskManager scheduleTaskManager,
            ILogger<ProcessExitHandler> logger)
        {
            _transactionDelegateCenter = transactionDelegateCenter;
            _logger = logger;
            _scheduleTaskManager = scheduleTaskManager;
        }
        public void Dispose()
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
        }

        public void Listen(MicroServiceHost microServiceProvider)
        {
            _microServiceHost = microServiceProvider;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _ProcessExited = true;
            _logger?.LogInformation("准备断开网关");
            List<Task> tasks = new List<Task>();
            lock(_missions)
            {
                foreach( var action in _missions )
                {
                    try
                    {
                        tasks.Add(Task.Run(action));
                    }
                    catch
                    {
                    }
                }
                _missions.Clear();
            }

            try
            {
                var client = new NetClient(_microServiceHost.MasterGatewayAddress);
                client.WriteServiceData(new GatewayCommand { 
                    Type = CommandType.UnRegisterSerivce,
                    Content = _microServiceHost.Id
                });
                client.ReadServiceObject<InvokeResult>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, ex.Message);
            }

            _logger?.LogInformation("等待事务托管中心事务清零");
            while (_transactionDelegateCenter.List.Count > 0)
                Thread.Sleep(1000);

            _logger?.LogInformation("停止所有定时任务");
            _scheduleTaskManager.StopTasks();

            _logger?.LogInformation("等待客户端请求数清零");
            //等待客户连接处理完毕
            while (_microServiceHost.ClientConnected > 0)
                Thread.Sleep(1000);

            _logger?.LogInformation("客户端请求数为零，等待IProcessExitHandler任务执行完毕");

            Task.WaitAll(tasks.ToArray());
            Thread.Sleep(1000);
        }

        public void AddHandler(Action action)
        {
            lock (_missions)
            {
                _missions.Add(action);
            }
        }

        public void RemoveHandler(Action action)
        {
            lock (_missions)
            {
               for(int i = 0; i < _missions.Count; i ++)
                {
                    var item = _missions[i];
                    if(item.Target == action.Target && item.Method == action.Method)
                    {
                        _missions.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
    }
}
