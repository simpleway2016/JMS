using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.ServerCore.Http
{
    public class HttpResult
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="statusCode">http status code，当值大于等于400，则被视为执行失败，事务也会自动回滚</param>
        /// <param name="data"></param>
        public HttpResult(int statusCode , object data)
        {
            StatusCode = statusCode;
            Data = data;
        }

        /// <summary>
        /// http status code，当值大于等于400，则被视为执行失败，事务也会自动回滚
        /// </summary>
        public int StatusCode { get; }
        public object Data { get; }
    }
}
