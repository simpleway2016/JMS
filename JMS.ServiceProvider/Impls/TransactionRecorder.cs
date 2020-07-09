using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;

namespace JMS
{
    public class TransactionRecorderOption
    {
        public string TransactionLogFolder { get; set; }
    }
    public static class TransactionRecorderOptionExtension
    {
        internal static TransactionRecorderOption Option = new TransactionRecorderOption();
        public static MicroServiceHost UseTransactionRecorder( this MicroServiceHost host, Action<TransactionRecorderOption> action)
        {
            host._services.AddSingleton<ITransactionRecorder, TransactionRecorder>();
            if (action != null)
                action(Option);
            return host;
        }
    }
    class TransactionRecorder : ITransactionRecorder
    {
        ILogger<TransactionRecorder> _logger;
        ConcurrentQueue<TransactionDelegate> Caches = new ConcurrentQueue<TransactionDelegate>();
        AutoResetEvent _waitForObject = new AutoResetEvent(false);
        Way.Lib.FileLogger _fileLogger;
        MicroServiceHost _microServiceHost;
        public TransactionRecorder(ILogger<TransactionRecorder> logger, MicroServiceHost microServiceHost)
        {
            _microServiceHost = microServiceHost;
            if (string.IsNullOrEmpty(TransactionRecorderOptionExtension.Option.TransactionLogFolder))
                return;

            _fileLogger = new FileLogger(TransactionRecorderOptionExtension.Option.TransactionLogFolder, "log");
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
                        FileLogger.DeleteFiles(TransactionRecorderOptionExtension.Option.TransactionLogFolder, DateTime.Now.AddDays(-10));
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
            if (_fileLogger == null)
                return;

            Caches.Enqueue(tranDelegate);
            _waitForObject.Set();
        }
    }
}
