using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.ServerCore.Http
{
    public class HttpResult
    {
        public HttpResult(int statusCode , object data)
        {
            StatusCode = statusCode;
            Data = data;
        }

        public int StatusCode { get; }
        public object Data { get; }
    }
}
