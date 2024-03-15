using JMS.WebApiDocument;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest
{
    internal class TestServiceActionFilter : IServiceActionFilter
    {
        public Task OnAction(InvokingContext context)
        {
            return Task.CompletedTask;
        }
    }
}
