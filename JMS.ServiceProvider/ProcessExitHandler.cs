using JMS.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace JMS
{
    /// <summary>
    /// 关于进程退出时的处理,linux如果要关闭进程，需要使用 "kill -15 进程id" 命令，这样，ProcessExitHandler才能处理
    /// </summary>
    class ProcessExitHandler :IDisposable
    {
        public MicroServiceHost _microServiceHost;
        TransactionDelegateCenter _transactionDelegateCenter;
        public bool ProcessExited = false;
        ILogger<ProcessExitHandler> _logger;
        ScheduleTaskManager _scheduleTaskManager;
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
            ProcessExited = true;
            _logger?.LogInformation("准备断开网关");
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

            _logger?.LogInformation("客户端请求数为零，当前进程退出");
            Thread.Sleep(1000);
        }
    }
}
