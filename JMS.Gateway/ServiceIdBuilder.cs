using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace JMS
{
    class ServiceIdBuilder
    {
        int CurrentId = 0;
        string _FilePath;
        public ServiceIdBuilder(IConfiguration configuration)
        {
            var datafolder = configuration.GetValue<string>("DataFolder");
            _FilePath = $"{datafolder}/ServiceIdBuilder.txt";
            if (File.Exists(_FilePath))
            {
                this.CurrentId = Convert.ToInt32(File.ReadAllText(_FilePath ,Encoding.UTF8));
            }
        }

        public int Build()
        {
            lock(this)
            {
                CurrentId++;
                File.WriteAllText(_FilePath, CurrentId.ToString(), Encoding.UTF8);
            }
            return CurrentId;
        }
    }
}
