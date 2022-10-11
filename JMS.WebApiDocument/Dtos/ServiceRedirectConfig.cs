using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.WebApiDocument.Dtos
{
    internal class ServiceRedirectConfig
    {
        public string ServiceName { get; set; }
        public string Description { get; set; }
        public ServiceRedirectButtonConfig[] Buttons { get; set; }
    }

    internal class ServiceRedirectButtonConfig
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
