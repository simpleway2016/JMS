using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.WebApiDocument
{
    internal class ServiceRedirects
    {
        public static ServiceRedirectConfig[] Configs;
        public static Func<RemoteClient> ClientProviderFunc;

        /// <summary>
        /// 调用微服务方法
        /// </summary>
        /// <param name="config"></param>
        /// <param name="context"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        internal static async Task<object> InvokeServiceMethod(ServiceRedirectConfig config,HttpContext context,string method, string[] redirectHeaders)
        {

            byte[] postContent = null;
            if (context.Request.ContentLength != null)
            {
                postContent = new byte[(int)context.Request.ContentLength];
                await context.Request.Body.ReadAsync(postContent, 0, postContent.Length);
            }

            object[] _parames = null;
            if (postContent != null)
            {
                var json = Encoding.UTF8.GetString(postContent);
                _parames = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(json);
            }
            using (var client = ClientProviderFunc())
            {
                if (redirectHeaders == null)
                {
                    foreach (var header in context.Request.Headers)
                    {
                        if (header.Key == "TranId")
                            continue;
                        else if (header.Key == "Tran")
                            continue;
                        else if (header.Key == "TranFlag")
                            continue;

                        client.SetHeader(header.Key, header.Value.ToString());
                    }
                }
                else
                {
                    foreach (var header in redirectHeaders)
                    {
                        if (context.Request.Headers.TryGetValue(header, out StringValues value))
                        {
                            client.SetHeader(header, value.ToString());
                        }
                    }
                }
                var service = client.TryGetMicroService(config.ServiceName);
                if (_parames == null)
                {
                    return await service.InvokeAsync<object>(method);
                }
                else
                    return await service.InvokeAsync<object>(method, _parames);
            }
        }
    }
}
