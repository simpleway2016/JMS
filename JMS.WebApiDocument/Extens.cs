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
        static ILogger<JMS.WebApiDocument.WebApiDocAttribute> Logger;
        /// <summary>
        /// 
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
                }
                return next();
            });
            return app;
        }

        /// <summary>
        /// 启用微服务的反向代理
        /// </summary>
        /// <param name="app"></param>
        /// <param name="clientProviderFunc">RemoteClient创建函数</param>
        /// <returns></returns>
        public static IApplicationBuilder UseJmsServiceRedirect(this IApplicationBuilder app, Func<RemoteClient> clientProviderFunc)
        {
            return UseJmsServiceRedirect(app, null, clientProviderFunc, null);
        }
        /// <summary>
        /// 根据配置文件的JMS.ServiceRedirects节点配置，直接转接访问到指定的微服务
        /// </summary>
        /// <param name="app"></param>
        /// <param name="configuration">配置信息,如果为null，则表示所有允许网关反向代理的微服务</param>
        /// <param name="clientProviderFunc">RemoteClient创建函数</param>
        /// <param name="redirectHeaders">需要转发的请求头，默认null，表示转发全部</param>
        /// <returns></returns>
        [Obsolete]
        public static IApplicationBuilder UseJmsServiceRedirect(this IApplicationBuilder app, IConfiguration configuration, Func<RemoteClient> clientProviderFunc, string[] redirectHeaders = null)
        {
            if (clientProviderFunc == null)
                throw new Exception("clientProviderFunc is null");

            Logger = app.ApplicationServices.GetService<ILogger<WebApiDocAttribute>>();

            ServiceRedirects.ClientProviderFunc = clientProviderFunc;
            if (configuration != null)
            {
                ConfigurationChangeCallback(configuration);
            }

            ThreadPool.GetMaxThreads(out int w, out int c);
            ThreadPool.SetMinThreads(w, c);

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
                    var config = ServiceRedirects.Configs?.FirstOrDefault(n => string.Equals(n.ServiceName, servicename, StringComparison.OrdinalIgnoreCase));
                    if (ServiceRedirects.Configs != null && config == null)
                    {
                        context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
                        await context.Response.WriteAsync(new
                        {
                            code = 404,
                            msg = $"请检查[JMS.ServiceRedirects]节点里是否配置了{servicename}"
                        }.ToJsonString());
                        return;

                    }
                    else if(config == null)
                    {
                        config = new ServiceRedirectConfig() { 
                            ServiceName = servicename,
                            OutputText = true
                        };
                    }

                    try
                    {
                        var ret = await ServiceRedirects.InvokeServiceMethod(config, context, method, redirectHeaders);
                        if (config.Handled)
                            return;

                        if (config.OutputText)
                        {
                            if (ret is string)
                            {
                                context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
                                await context.Response.WriteAsync((string)ret);
                            }
                            else if (ret != null)
                            {                              
                                if (ret.GetType().IsValueType)
                                {
                                    context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
                                    await context.Response.WriteAsync(ret.ToString());
                                }
                                else
                                {
                                    context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
                                    await context.Response.WriteAsync(ret.ToJsonString());
                                }
                            }

                        }
                        else
                        {
                            context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
                            await context.Response.WriteAsync(new
                            {
                                code = 200,
                                data = ret
                            }.ToJsonString());
                        }

                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null)
                            ex = ex.InnerException;
                        if (config.OutputText)
                        {
                            context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
                            if (ex.Message == "Authentication failed")
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
                        else
                        {
                            context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
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
                    }
                    return;
                }
                await next();
            });


            return app;
        }

        static IDisposable CallbackRegistration;
        static void ConfigurationChangeCallback(object p)
        {
            CallbackRegistration?.Dispose();
            CallbackRegistration = null;

            IConfiguration configuration = (IConfiguration)p;
            try
            {
                var configs = configuration.GetSection("JMS.ServiceRedirects").Get<ServiceRedirectConfig[]>();
                if (configs == null)
                {
                    throw new Exception("配置文件中找不到有效的JMS.ServiceRedirects节点");
                }

                Logger?.LogInformation($"读取配置文件JMS.ServiceRedirects 信息");

                foreach (var config in configs)
                {
                    if (config.Buttons != null)
                    {
                        foreach (var btn in config.Buttons)
                        {
                            if (btn.Url != null)
                            {
                                btn.Url = btn.Url.Replace("$ServiceName$", HttpUtility.UrlEncode(config.ServiceName));
                            }
                        }
                    }
                }
                ServiceRedirects.Configs = configs;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Task.Run(() =>
                {
                    Thread.Sleep(1000);//延迟注册，否则可能每次都回调两次
                    CallbackRegistration = configuration.GetReloadToken().RegisterChangeCallback(ConfigurationChangeCallback, configuration);
                });
            }

        }

    }
}
