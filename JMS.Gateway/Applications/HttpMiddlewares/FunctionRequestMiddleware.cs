using JMS.Dtos;
using JMS.ServerCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Way.Lib;

namespace JMS.Applications.HttpMiddlewares
{
    internal class FunctionRequestMiddleware : IHttpMiddleware
    {
        ICommandHandlerRoute _commandHandlerRoute;
        public FunctionRequestMiddleware(ICommandHandlerRoute commandHandlerRoute)
        {
            this._commandHandlerRoute = commandHandlerRoute;

        }
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IDictionary<string, string> headers)
        {
            if (requestPath.StartsWith("/?GetServiceProvider=") || requestPath.StartsWith("/?GetAllServiceProviders") || requestPath.StartsWith("/?FindMaster"))
            {

                requestPath = requestPath.Substring(2);
                if (requestPath.Contains("="))
                {
                    var arr = requestPath.Split('=');
                    var json = HttpUtility.UrlDecode(arr[1], Encoding.UTF8);
                    if (!json.StartsWith("{"))
                    {
                        json = new GetServiceProviderRequest { ServiceName = json }.ToJsonString();
                    }
                    var cmd = new GatewayCommand
                    {
                        Type = (CommandType)Enum.Parse(typeof(CommandType), arr[0]),
                        Content = json,
                        Header = headers,
                        IsHttp = true
                    };
                    await _commandHandlerRoute.AllocHandler(cmd)?.Handle(client, cmd);
                }
                else
                {
                    var cmd = new GatewayCommand
                    {
                        Type = (CommandType)Enum.Parse(typeof(CommandType), requestPath),
                        Header = headers,
                        IsHttp = true
                    };
                    await _commandHandlerRoute.AllocHandler(cmd)?.Handle(client, cmd);
                }

                return true;
            }
            return false;
        }
    }
}
