using JMS.Dtos;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS.WebApiDocument
{
    public interface IServiceActionFilter
    {
        Task OnAction(InvokingContext context);
    }

    public class InvokingContext
    {
        public ClientServiceDetail ServiceDetail { get; set; }
        public IHeaderDictionary Headers { get; internal set; }
        public string Method { get; set; }
        public object[] Parameters { get; set; }
    }
}
