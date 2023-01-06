using JMS;
using JMS.WebApiDocument;
using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
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
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="xmlpaths">包含注释的xml文档路径</param>
        /// <returns></returns>
        public static IApplicationBuilder UseJMSWebApiDocument(this IApplicationBuilder app)
        {

            app.Use((context, next) =>
            {
                if (context.Request.Path.Value.EndsWith("/JmsDoc", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var manager = app.ApplicationServices.GetService<ApplicationPartManager>();
                        List<Type> controllerTypes = new List<Type>();
                        foreach (var part in manager.ApplicationParts)
                        {
                            if (part is AssemblyPart assemblyPart)
                            {
                                controllerTypes.AddRange(assemblyPart.Types.Where(m => m.GetCustomAttribute<WebApiDocAttribute>() != null).ToArray());
                            }
                        }

                        return HtmlBuilder.Build(context, controllerTypes);
                    }
                    catch (Exception ex)
                    {
                        return context.Response.WriteAsync(ex.ToString());
                    }
                }
                else if (context.Request.Path.Value.EndsWith("/JmsDoc/vue.js", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.Request.Headers.ContainsKey("If-Modified-Since"))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                        return context.Response.WriteAsync("");
                    }
                    context.Response.ContentType = "text/javascript; charset=utf-8";
                    context.Response.Headers.Add("Last-Modified", "Fri , 12 May 2006 18:53:33 GMT");
                    using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.vue.js"))
                    {
                        var bs = new byte[ms.Length];
                        ms.Read(bs, 0, bs.Length);
                        var text = Encoding.UTF8.GetString(bs);

                        return context.Response.WriteAsync(text);
                    }
                }
                return next();
            });
            return app;
        }

        /// <summary>
        /// 根据配置文件的JMS.ServiceRedirects节点配置，直接转接访问到指定的微服务
        /// </summary>
        /// <param name="app"></param>
        /// <param name="configuration">配置信息</param>
        /// <param name="clientProviderFunc">RemoteClient创建函数</param>
        /// <param name="redirectHeaders">需要转发的请求头，默认null，表示转发全部</param>
        /// <returns></returns>
        public static IApplicationBuilder UseJmsServiceRedirect(this IApplicationBuilder app, IConfiguration configuration, Func<RemoteClient> clientProviderFunc , string[] redirectHeaders = null)
        {
            if (clientProviderFunc == null)
                throw new Exception("clientProviderFunc is null");


            configuration.GetReloadToken().RegisterChangeCallback(ConfigurationChangeCallback, configuration);

            ServiceRedirects.ClientProviderFunc = clientProviderFunc;
            ConfigurationChangeCallback(configuration);

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Value.Contains("/JMSRedirect/", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
                    try
                    {
                        var m = Regex.Match(context.Request.Path.Value, @"\/JMSRedirect\/(?<s>((?![\/]).)+)/(?<m>\w+)");
                        var servicename = m.Groups["s"].Value;
                        var method = m.Groups["m"].Value;
                        var config = ServiceRedirects.Configs?.FirstOrDefault(m => string.Equals(m.ServiceName, servicename, StringComparison.OrdinalIgnoreCase));
                        if (config == null)
                        {
                            await context.Response.WriteAsync(new
                            {
                                code = 404,
                                msg = "Service not found"
                            }.ToJsonString());
                        }
                        else
                        {
                            var ret = await ServiceRedirects.InvokeServiceMethod(config, context, method,redirectHeaders);
                            if (config.OutputText)
                            {
                                if (ret is string)
                                {
                                    context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
                                    await context.Response.WriteAsync((string)ret);
                                }
                                else if(ret != null)
                                {
                                    await context.Response.WriteAsync(ret.ToJsonString());
                                }

                            }
                            else {
                                
                                await context.Response.WriteAsync(new
                                {
                                    code = 200,
                                    data = ret
                                }.ToJsonString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null)
                            ex = ex.InnerException;

                        if (ex.Message == "Authentication failed")
                        {
                            await context.Response.WriteAsync(new
                            {
                                code = 401,
                                msg = ex.Message
                            }.ToJsonString());
                        }
                        else
                        {
                            await context.Response.WriteAsync(new
                            {
                                code = 500,
                                msg = ex.Message
                            }.ToJsonString());
                        }
                    }
                    return;
                }
                await next();
            });
          

            return app;
        }

        static void ConfigurationChangeCallback(object p)
        {
            IConfiguration configuration = (IConfiguration)p;
            configuration.GetReloadToken().RegisterChangeCallback(ConfigurationChangeCallback, configuration);

            var configs = configuration.GetSection("JMS.ServiceRedirects").Get<ServiceRedirectConfig[]>();
            if(configs == null)
            {
                throw new Exception("配置文件中找不到有效的JMS.ServiceRedirects节点");
            }
            foreach( var config in configs)
            {
                if(config.Buttons != null)
                {
                    foreach( var btn in config.Buttons)
                    {
                        if(btn.Url != null)
                        {
                            btn.Url = btn.Url.Replace("$ServiceName$", HttpUtility.UrlEncode(config.ServiceName));
                        }
                    }
                }
            }
            ServiceRedirects.Configs = configs;
        }

    }
}
