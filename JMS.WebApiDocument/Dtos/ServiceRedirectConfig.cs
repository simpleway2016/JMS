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
        public bool OutputText { get; set; }
        /// <summary>
        /// 是否已经被内部程序接管
        /// </summary>
        internal bool Handled { get; set; }
        public ServiceRedirectButtonConfig[] Buttons { get; set; }
    }

    internal class ServiceRedirectButtonConfig
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
