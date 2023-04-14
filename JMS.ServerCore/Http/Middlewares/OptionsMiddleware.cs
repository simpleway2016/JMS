using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS.ServerCore.Http.Middlewares
{
    public class OptionsMiddleware : IHttpMiddleware
    {
        public Task<bool> Handle(NetClient netClient, string httpMethod, string requestPath,IDictionary<string,string> headers)
        {
            if(httpMethod == "OPTIONS")
            {
                netClient.OutputHttp204(headers);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
