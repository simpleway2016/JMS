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
        public MicroServiceHost _microServiceProvider;
        TransactionDelegateCenter _transactionDelegateCenter;
        public bool ProcessExited = false;
        ILogger<ProcessExitHandler> _logger;
        public ProcessExitHandler(TransactionDelegateCenter transactionDelegateCenter, ILogger<ProcessExitHandler> logger)
        {
            _transactionDelegateCenter = transactionDelegateCenter;
            _logger = logger;
        }
        public void Dispose()
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
        }

        public void Listen(MicroServiceHost microServiceProvider)
        {
            _microServiceProvider = microServiceProvider;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            ProcessExited = true;
            _logger?.LogInformation("进程即将被终止");
            _microServiceProvider.DisconnectGateway();
            _transactionDelegateCenter.RollbackAll();

            _logger?.LogInformation("TransactionDelegateCenter RollbackAll 完毕");
            //等待客户连接处理完毕
            while (_microServiceProvider.ClientConnected > 0)
                Thread.Sleep(1000);

            _logger?.LogInformation("ClientConnected 为零，当前进程退出");
        }
    }
}
