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
      
        public TransactionRecorder(ILogger<TransactionRecorder> logger)
        {
            _logger = logger;
            new Thread(runForWriteFile).Start();
        }

        void runForWriteFile()
        {
            while(true)
            {
                try
                {
                    _waitForObject.WaitOne();
                    using (var clog = new CLog("./TransactionLogs/Log", false))
                    {
                        while (Caches.TryDequeue(out TransactionDelegate item))
                        {
                            clog.Log("TranId:{0} Submit Content:{1}", item.TransactionId, item.RequestCommand.ToJsonString());
                        }
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
