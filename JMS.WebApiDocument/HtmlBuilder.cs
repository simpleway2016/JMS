using JMS.AssemblyDocumentReader;
using JMS.Common;
using JMS.Dtos;
using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Way.Lib;
using static Org.BouncyCastle.Math.EC.ECCurve;
using static System.Net.Mime.MediaTypeNames;

namespace JMS.WebApiDocument
{
    public class HtmlBuilder
    {
        public static async Task OutputCode(HttpContext context)
        {
            var requestPath = context.Request.Path.Value;
            var index = requestPath.IndexOf("/JmsDoc/OutputCode/", StringComparison.OrdinalIgnoreCase);
            var servicename = requestPath.Substring(index + 19);

            var buttonName = context.Request.Query["button"].ToString();

            using (var rc = RequestHandler.ClientProviderFunc())
            {
                var service = await rc.TryGetMicroServiceAsync(servicename);
                if (service == null)
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync($"Service:{servicename} does not exist");
                }

                if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.JmsService)
                {
                    var jsonContent = service.GetServiceInfo();
                    var buttons = await rc.GetApiDocumentButtons<ApiDocCodeBuilderInfo>(buttonName);

                    var controllerInfo = jsonContent.FromJson<ControllerInfo>();
                    controllerInfo.name = servicename;

                    ControllerInfo.FormatForBuildCode(controllerInfo, servicename);

                    using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.outputCode.html"))
                    {
                        var bs = new byte[ms.Length];
                        ms.Read(bs, 0, bs.Length);
                        var code = buttons.FirstOrDefault(m => m.Name == buttonName).Code;
                        var vueMethods = buttons.FirstOrDefault(m => m.Name == "vue methods")?.Code;
                        var text = Encoding.UTF8.GetString(bs).Replace("$$Controller$$", controllerInfo.ToJsonString()).Replace("$$code$$", code).Replace("$$vueMethods$$", vueMethods);

                        bs = GZipHelper.Compress( Encoding.UTF8.GetBytes(text));

                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.Headers.Add("Content-Encoding", "gzip");
                        context.Response.ContentLength = bs.Length;

                        await context.Response.Body.WriteAsync(bs,0, bs.Length);
                    }
                }
            }

        }

        public static async Task OutputSse(HttpContext context)
        {
            var response = context.Response;
            response.Headers["Content-Type"] = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["Connection"] = "keep-alive";

            int lockflag = 0;
            List<ServiceDetail> doneList = new List<ServiceDetail>();
            List<IMicroService> services = new List<IMicroService>();
            using (var client = RequestHandler.ClientProviderFunc())
            {
                ApiDocCodeBuilderInfo[] buttons;
                try
                {
                    buttons = await client.GetApiDocumentButtons<ApiDocCodeBuilderInfo>();
                }
                catch
                {
                    buttons = new ApiDocCodeBuilderInfo[0];
                }
                var allServices = await client.ListMicroServiceAsync(null);

                foreach (var serviceRunningInfo in allServices)
                {
                    foreach (var serviceInfo in serviceRunningInfo.ServiceList)
                    {
                        if (doneList.Any(m => m.Name == serviceInfo.Name))
                            continue;

                        doneList.Add(serviceInfo);
                    }
                }

                foreach (var serviceInfo in doneList)
                {
                    var service = await client.TryGetMicroServiceAsync(serviceInfo.Name);
                    if (service != null)
                        services.Add(service);
                }

                ConcurrentQueue<string> pendingOutputs = new ConcurrentQueue<string>();

                await Parallel.ForEachAsync(services, async (service, cancelToken) =>
                {
                    try
                    {
                        ControllerInfo controllerInfo = null;

                        if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.JmsService)
                        {
                            var jsonContent = await service.GetServiceInfoAsync();
                            controllerInfo = jsonContent.FromJson<ControllerInfo>();
                            controllerInfo.isPrivate = !service.ServiceLocation.AllowGatewayProxy;
                            if (!string.IsNullOrWhiteSpace(service.ServiceLocation.Description))
                            {
                                controllerInfo.desc = service.ServiceLocation.Description;
                            }

                            controllerInfo.buttons = buttons.Where(m => m.Name != "vue methods").Select(m => new ButtonInfo
                            {
                                name = m.Name
                            }).ToList();
                            foreach (var btn in controllerInfo.buttons)
                            {
                                btn.url += $"JmsDoc/OutputCode/{service.ServiceLocation.Name}?button={HttpUtility.UrlEncode(btn.name)}";
                            }

                            foreach (var method in controllerInfo.items)
                            {
                                method.url = $"/JMSRedirect/{HttpUtility.UrlEncode(service.ServiceLocation.Name)}/{method.title}";
                            }
                            if (controllerInfo.items.Count == 1)
                                controllerInfo.items[0].opened = true;

                        }
                        else if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.WebSocket)
                        {
                            var jsonContent = await service.GetServiceInfoAsync();
                            var cInfo = jsonContent.FromJson<ControllerInfo>();

                            controllerInfo = new ControllerInfo()
                            {
                                name = service.ServiceLocation.Name,
                                desc = string.IsNullOrWhiteSpace(service.ServiceLocation.Description) ? service.ServiceLocation.Name : service.ServiceLocation.Description,
                            };
                            controllerInfo.items = new List<MethodItemInfo>();
                            controllerInfo.items.Add(new MethodItemInfo
                            {
                                title = "WebSocket接口",
                                method = cInfo.desc,
                                isComment = true,
                                isWebSocket = true,
                                opened = true,
                                url = $"/JMSRedirect/{HttpUtility.UrlEncode(service.ServiceLocation.Name)}"
                            });

                        }

                        if (controllerInfo != null)
                        {
                            var outputContent = Convert.ToBase64String(GZipHelper.Compress(Encoding.UTF8.GetBytes(controllerInfo.ToJsonString())));
                            outputContent = $"data: {outputContent}\n\n";
                            if (Interlocked.CompareExchange(ref lockflag, 1, 0) == 0)
                            {
                                await context.Response.WriteAsync(outputContent);
                                if (pendingOutputs.Count > 0)
                                {
                                    while (pendingOutputs.TryDequeue(out outputContent))
                                    {
                                        await context.Response.WriteAsync(outputContent);
                                    }
                                }
                                lockflag = 0;
                            }
                            else
                            {
                                pendingOutputs.Enqueue(outputContent);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        context.RequestServices.GetService<ILogger<JMS.WebApiDocument.HtmlBuilder>>()?.LogError(ex, "");
                    }
                });

                if (pendingOutputs.Count > 0)
                {
                    while (pendingOutputs.TryDequeue(out string outputContent))
                    {
                        await context.Response.WriteAsync(outputContent);
                    }
                }

                await context.Response.WriteAsync($"data: ok\n\n");
            }
        }

        public static async Task OutputIndex(HttpContext context)
        {
            context.Response.Headers.Add("Content-Encoding", "gzip");

            using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.index.html"))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                var bs = new byte[ms.Length];
                ms.Read(bs, 0, bs.Length);
                bs = GZipHelper.Compress(bs);
                context.Response.ContentLength = bs.Length;

                 await context.Response.Body.WriteAsync(bs,0,bs.Length);
            }
        }

    }
}
