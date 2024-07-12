using JMS;
using JMS.Common;
using JMS.WebApiDocument;
using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Way.Lib;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class Extens
    {
       
        /// <summary>
        /// 启用接口文档 /jmsdoc 页面
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseJMSWebApiDocument(this IApplicationBuilder app)
        {

            app.Use((context, next) =>
            {
                if (context.Request.Path.Value.Contains("/JmsDoc", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.Request.Path.Value.Contains("/jmsdoc.vue.pako.js", StringComparison.OrdinalIgnoreCase))
                    {
                        DateTime dateTime = new DateTime(2023, 5, 12, 18, 53, 33);

                        if (context.Request.Headers.TryGetValue("If-Modified-Since", out StringValues sinceTime) && Convert.ToDateTime(sinceTime.ToString()) == dateTime)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                            return context.Response.WriteAsync("");
                        }

                        string formattedDateTime = dateTime.ToUniversalTime().ToString("r");

                        context.Response.ContentType = "text/javascript; charset=utf-8";
                        context.Response.Headers.Add("Last-Modified", formattedDateTime);
                        context.Response.Headers.Add("Content-Encoding", "gzip");

                        using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.jmsdoc.vue.pako.js"))
                        {
                            var bs = new byte[ms.Length];
                            ms.Read(bs, 0, bs.Length);
                            bs = GZipHelper.Compress(bs);
                            context.Response.ContentLength = bs.Length;

                            return context.Response.Body.WriteAsync(bs, 0, bs.Length);
                        }
                    }
                    else if (context.Request.Path.Value.Contains("/JmsDoc/OutputCode/", StringComparison.OrdinalIgnoreCase))
                    {
                        return HtmlBuilder.OutputCode(context);
                    }
                    else if (context.Request.Path.Value.EndsWith("/JmsDocSse", StringComparison.OrdinalIgnoreCase))
                    {
                        return HtmlBuilder.OutputSse(context);
                    }
                    else if (context.Request.Path.Value.EndsWith("/JmsDoc", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            return HtmlBuilder.OutputIndex(context);
                        }
                        catch (Exception ex)
                        {
                            return context.Response.WriteAsync(ex.ToString());
                        }
                    }
                }
                return next();
            });
            return app;
        }


        /// <summary>
        /// 启用JMSFramework WebApi服务
        /// </summary>
        /// <param name="app"></param>
        /// <param name="clientProviderFunc">RemoteClient创建函数</param>
        /// <returns></returns>
        public static IApplicationBuilder UseJmsServiceRedirect(this IApplicationBuilder app, Func<RemoteClient> clientProviderFunc)
        {
            if (clientProviderFunc == null)
                throw new Exception("clientProviderFunc is null");


            RequestHandler.ClientProviderFunc = clientProviderFunc;

            ThreadPool.SetMinThreads(Environment.ProcessorCount*10, Environment.ProcessorCount * 10);

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Value.Contains("/JMSRedirect/", StringComparison.OrdinalIgnoreCase))
                {
                    
                    var m = Regex.Match(context.Request.Path.Value, @"\/JMSRedirect\/(?<s>((?![\/]).)+)/(?<m>\w+)");
                    if(m.Length == 0)
                    {
                        m = Regex.Match(context.Request.Path.Value, @"\/JMSRedirect\/(?<s>((?![\/]).)+)");
                    }
                    var servicename = m.Groups["s"].Value;
                    var method = m.Groups["m"].Value;
                   

                    try
                    {
                        await RequestHandler.InvokeServiceMethod(servicename, context, method);                       

                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null)
                            ex = ex.InnerException;

                        context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
                        if (ex is OperationCanceledException)
                        {
                            context.Response.StatusCode = 408;
                            await context.Response.WriteAsync("");
                        }
                        else if (ex.Message == "Authentication failed")
                        {
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync(ex.Message);
                        }
                        else
                        {
                            context.Response.StatusCode = 500;
                            await context.Response.WriteAsync(ex.Message);
                        }
                    }
                    return;
                }
                await next();
            });


            return app;
        }


    }
}
