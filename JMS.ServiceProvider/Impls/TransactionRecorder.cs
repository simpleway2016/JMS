using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS.Impls
{
    class TransactionRecorder : ITransactionRecorder
    {
        ILogger<TransactionRecorder> _logger;
        ConcurrentQueue<TransactionDelegate> Caches = new ConcurrentQueue<TransactionDelegate>();
        AutoResetEvent _waitForObject = new AutoResetEvent(false);
        Way.Lib.FileLogger _fileLogger;
        public TransactionRecorder(ILogger<TransactionRecorder> logger)
        {
            _fileLogger = new FileLogger("./TransactionLogs", "log");
            _logger = logger;
            new Thread(runForWriteFile).Start();
        }

        void runForWriteFile()
        {
            DateTime checktime = DateTime.Now;
            while(true)
            {
                try
                {
                    _waitForObject.WaitOne();
                    while (Caches.TryDequeue(out TransactionDelegate item))
                    {
                        _fileLogger.Log("TranId:{0} Submit Content:{1}", item.TransactionId, item.RequestCommand.ToJsonString());
                    }

                    if((DateTime.Now - checktime).TotalDays > 1)
                    {
                        checktime = DateTime.Now;
                        FileLogger.DeleteFiles("./TransactionLogs", DateTime.Now.AddDays(-10));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                }
            }
        }

        public void Record(TransactionDelegate tranDelegate)
        {
            Caches.Enqueue(tranDelegate);
            _waitForObject.Set();
        }
    }
}
