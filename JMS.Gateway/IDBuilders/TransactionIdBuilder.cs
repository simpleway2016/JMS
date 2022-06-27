using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace JMS
{
    class TransactionIdBuilder
    {
        static int CurrentId = 0;
        ILogger<TransactionIdBuilder> _logger;
        AutoResetEvent _waitObject = new AutoResetEvent(false);
        string _filepath;
        public TransactionIdBuilder(IConfiguration configuration, ILogger<TransactionIdBuilder> logger)
        {
            _logger = logger;
            var datafolder = configuration.GetValue<string>("DataFolder");
            _filepath = $"{datafolder}/TransactionIdBuilder.txt";
            if (File.Exists(_filepath))
            {
                CurrentId = Convert.ToInt32(File.ReadAllText(_filepath, Encoding.UTF8));
            }
            new Thread(saveToFile).Start();
        }
        void saveToFile()
        {
            while(true)
            {
                try
                {
                    _waitObject.WaitOne();
                    File.WriteAllText(_filepath, CurrentId.ToString(), Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                }
            }
        }
        public string Build()
        {
            var ret = Interlocked.Increment(ref CurrentId).ToString();
            _waitObject.Set();
            return $"{DateTime.Now.Ticks}-{ret}";
        }
    }
}
