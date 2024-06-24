using Org.BouncyCastle.Ocsp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    public abstract class WebSocketController: BaseJmsController
    {      

        string _requestPath;
        /// <summary>
        /// 请求路径
        /// </summary>
        public string RequestPath
        {
            get
            {
                if (_requestPath == null && RequestingObject.Value != null)
                {
                    _requestPath = RequestingObject.Value.RequestPath;
                }
                return _requestPath;
            }
        }

        NameValueCollection _RequestQuery;
        /// <summary>
        /// 客户端请求的Query
        /// </summary>
        public NameValueCollection RequestQuery
        {
            get
            {
                if (_RequestQuery == null && RequestingObject.Value != null)
                {
                    _RequestQuery = RequestingObject.Value.RequestQuery;
                }
                return _RequestQuery;
            }
        }

        public string SubProtocol
        {
            get;
            internal set;
        }

        public abstract Task OnConnected(WebSocket webSocket);
    }

   
}
