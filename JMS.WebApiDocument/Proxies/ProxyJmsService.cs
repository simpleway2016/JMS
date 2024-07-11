using JMS.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.WebApiDocument.Proxies
{
    internal class ProxyJmsService
    {
        /// <summary>
        /// 调用微服务方法
        /// </summary>
        /// <param name="service"></param>
        /// <param name="context"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        internal static async Task Run(IMicroService service, HttpContext context, string method)
        {
            using (var client = RequestHandler.ClientProviderFunc())
            {
                byte[] postContent = null;
                if (context.Request.ContentLength != null && context.Request.ContentLength > 0)
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
                else if (context.Request.Query.ContainsKey("params"))
                {
                    var json = context.Request.Query["params"].ToString();
                    _parames = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(json);
                }

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


                var ip = context.Connection.RemoteIpAddress.ToString();
                if (client.TryGetHeader("X-Forwarded-For", out string xff))
                {
                    if (xff.Contains(ip) == false)
                    {
                        xff += $", {ip}";
                        client.SetHeader("X-Forwarded-For", xff);
                    }
                }
                else
                {
                    client.SetHeader("X-Forwarded-For", ip);
                }

                InvokeResult<object> result;
                if (_parames == null)
                {
                    result = await service.InvokeExAsync<object>(method);
                }
                else
                    result = await service.InvokeExAsync<object>(method, _parames);

                if (result == null)
                    return;

                InvokeAttributes invokeAttributes;

                if (result.Attributes != null)
                {
                    invokeAttributes = result.Attributes.FromJson<InvokeAttributes>();
                    if (invokeAttributes.StatusCode != null)
                    {
                        context.Response.StatusCode = invokeAttributes.StatusCode.Value;
                    }
                }
                if (result.Data is string)
                {
                    context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
                    await context.Response.WriteAsync((string)result.Data);
                }
                else if (result.Data != null)
                {
                    if (result.Data.GetType().IsValueType)
                    {
                        context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
                        await context.Response.WriteAsync(result.Data.ToString());
                    }
                    else
                    {
                        context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
                        await context.Response.WriteAsync(result.Data.ToJsonString());
                    }
                }
            }
        }
    }
}
