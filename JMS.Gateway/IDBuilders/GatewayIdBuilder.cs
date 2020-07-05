using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace JMS
{
    class GatewayIdBuilder
    {
        string CurrentId;
        string _FilePath;
        public GatewayIdBuilder(IConfiguration configuration)
        {
            var datafolder = configuration.GetValue<string>("DataFolder");
            _FilePath = $"{datafolder}/GatewayId.txt";
            if (File.Exists(_FilePath))
            {
                this.CurrentId = File.ReadAllText(_FilePath, Encoding.UTF8);
            }
            else
            {
                this.CurrentId = Guid.NewGuid().ToString("N");
                File.WriteAllText(_FilePath, this.CurrentId, Encoding.UTF8);
            }
        }

        public string Build()
        {            
            return CurrentId;
        }
    }
}
