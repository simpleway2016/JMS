using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.MicroServiceHost
{
    internal class ServiceException : Exception
    {
        public ServiceException(string message) : base(message)
        {

        }
    }
}
