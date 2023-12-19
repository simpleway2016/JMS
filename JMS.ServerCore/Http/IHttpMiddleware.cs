using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS.ServerCore.Http
{
    public interface IHttpMiddleware
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="netClient"></param>
        /// <param name="httpMethod"></param>
        /// <param name="requestPath"></param>
        /// <returns>false：继续处理下一个中间件，true：处理结束</returns>
        Task<bool> Handle(NetClient netClient, string httpMethod, string requestPath, Dictionary<string, string> headers);
    }
}
