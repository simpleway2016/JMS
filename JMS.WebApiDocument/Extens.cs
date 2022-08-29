using JMS.WebApiDocument;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

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
                if (context.Request.Path.Value.EndsWith( "/JmsDoc" , StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string[] xmlpaths = System.IO.Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.xml");
                        var membersEle = new XElement("members");
                        foreach (var path in xmlpaths)
                        {
                            try
                            {
                                var filename = System.IO.Path.GetFileNameWithoutExtension(path);
                                if (System.IO.File.Exists(filename + ".dll") == false)
                                    continue;

                                XDocument xdoc = XDocument.Load(path);
                                System.Xml.Linq.XElement xeRoot = xdoc.Root; //根节点 


                                var eleList = xeRoot.Element("members");
                                if (eleList != null)
                                {
                                    foreach (var item in eleList.Elements("member"))
                                    {
                                        membersEle.Add(item);
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }

                        var manager = app.ApplicationServices.GetService<ApplicationPartManager>();
                        List<Type> controllerTypes = new List<Type>();
                        foreach (var part in manager.ApplicationParts)
                        {
                            if (part is AssemblyPart assemblyPart)
                            {
                                controllerTypes.AddRange(assemblyPart.Types.Where(m => m.GetCustomAttribute<WebApiDocAttribute>() != null).ToArray());
                            }
                        }
                        return HtmlBuilder.Build(context, controllerTypes, membersEle);
                    }
                    catch (Exception ex)
                    {
                        return context.Response.WriteAsync(ex.ToString());
                    }
                }
                else if (context.Request.Path.Value.EndsWith("/JmsDoc/vue.js" , StringComparison.OrdinalIgnoreCase))
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

    }
}
