using JMS.Dtos;
using JMS.ServerCore.Http;
using JMS.WebApiDocument;
using JMS.WebApiDocument.Dtos;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Way.Lib;

namespace JMS.Applications.HttpMiddlewares
{
    internal class JmsDocMiddleware : IHttpMiddleware
    {
        async void outputCode(NetClient client, string httpMethod, string requestPath, IDictionary<string, string> headers)
        {

            var servicename = requestPath.Replace("/JmsDoc/OutputCode/", "");
            servicename = servicename.Substring(0, servicename.IndexOf("?"));

            var buttonName = requestPath.Substring(requestPath.IndexOf("?button=") + 8);
            buttonName = HttpUtility.UrlDecode(buttonName);

            using (var rc = new RemoteClient(WebApiProgram.GatewayAddresses))
            {
                var service = rc.TryGetMicroService(servicename);
                if (service == null)
                    throw new Exception($"Service:{servicename} does not exist");

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
                        client.OutputHttp200(text);
                    }
                }
            }


        }

        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IDictionary<string, string> headers)
        {
            if (requestPath.StartsWith("/JmsDoc", StringComparison.OrdinalIgnoreCase))
            {
                if (WebApiProgram.Configuration.GetSection("Http:SupportJmsDoc").Get<bool>() == false)
                {
                    client.OutputHttpNotFund();
                    return true;
                }

                if (requestPath.StartsWith("/JmsDoc/OutputCode/", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        outputCode(client, httpMethod, requestPath, headers);
                    }
                    catch (Exception ex)
                    {
                        client.OutputHttp200(ex.ToString());
                    }
                    return true;
                }
                else if (requestPath.StartsWith("/JmsDoc/vue.js", StringComparison.OrdinalIgnoreCase))
                {
                    if (headers.ContainsKey("If-Modified-Since"))
                    {
                        client.OutputHttpCode(304, "NotModified");
                        return true;
                    }

                    using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.vue.js"))
                    {
                        var bs = new byte[ms.Length];
                        ms.Read(bs, 0, bs.Length);
                        var text = Encoding.UTF8.GetString(bs);

                        client.OutputHttp200(text, "text/javascript", "Last-Modified: Fri , 12 May 2006 18:53:33 GMT");
                    }
                    return true;
                }

                List<ControllerInfo> controllerInfos = new List<ControllerInfo>();
                List<ServiceDetail> doneList = new List<ServiceDetail>();
                using (var rc = new RemoteClient(WebApiProgram.GatewayAddresses))
                {
                    ApiDocCodeBuilderInfo[] buttons;
                    try
                    {
                        buttons = await rc.GetApiDocumentButtons<ApiDocCodeBuilderInfo>();
                    }
                    catch
                    {
                        buttons = new ApiDocCodeBuilderInfo[0];
                    }

                    var allServiceInDoc = WebApiProgram.Configuration.GetSection("Http:AllServiceInDoc").Get<bool>();
                    var allServices = await rc.ListMicroServiceAsync(null);
                    foreach (var serviceRunningInfo in allServices)
                    {
                        foreach (var serviceInfo in serviceRunningInfo.ServiceList)
                        {
                            if ((serviceInfo.AllowGatewayProxy == false && allServiceInDoc == false) || doneList.Any(m => m.Name == serviceInfo.Name))
                                continue;
                            doneList.Add(serviceInfo);


                        }
                    }

                    Parallel.ForEach(doneList, serviceInfo =>
                    {
                        try
                        {
                            var service = rc.TryGetMicroService(serviceInfo.Name);
                            if (service == null)
                                return;

                            if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.JmsService)
                            {
                                var jsonContent = service.GetServiceInfo();
                                var controllerInfo = jsonContent.FromJson<ControllerInfo>();
                                controllerInfo.isPrivate = !serviceInfo.AllowGatewayProxy;
                                if (!string.IsNullOrWhiteSpace(serviceInfo.Description))
                                {
                                    controllerInfo.desc = serviceInfo.Description;
                                }
                                controllerInfo.buttons = buttons.Where(m => m.Name != "vue methods").Select(m => new ButtonInfo
                                {
                                    name = m.Name
                                }).ToList();
                                foreach (var btn in controllerInfo.buttons)
                                {
                                    btn.url += $"/JmsDoc/OutputCode/{serviceInfo.Name}?button={HttpUtility.UrlEncode(btn.name)}";
                                }
                                foreach (var method in controllerInfo.items)
                                {
                                    method.url = $"/{HttpUtility.UrlEncode(serviceInfo.Name)}/{method.title}";
                                }
                                if (controllerInfo.items.Count == 1)
                                    controllerInfo.items[0].opened = true;

                                lock (controllerInfos)
                                {
                                    controllerInfos.Add(controllerInfo);
                                }
                            }
                            else if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.WebSocket)
                            {
                                var jsonContent = service.GetServiceInfo();
                                var cInfo = jsonContent.FromJson<ControllerInfo>();

                                var controllerInfo = new ControllerInfo()
                                {
                                    name = serviceInfo.Name,
                                    isPrivate = !serviceInfo.AllowGatewayProxy,
                                    desc = string.IsNullOrWhiteSpace(serviceInfo.Description) ? serviceInfo.Name : serviceInfo.Description,
                                };
                                controllerInfo.items = new List<MethodItemInfo>();
                                controllerInfo.items.Add(new MethodItemInfo
                                {
                                    title = "WebSocket接口",
                                    method = cInfo.desc,
                                    isComment = true,
                                    isWebSocket = true,
                                    opened = true,
                                    url = $"/{HttpUtility.UrlEncode(serviceInfo.Name)}"
                                });
                                lock (controllerInfos)
                                {
                                    controllerInfos.Add(controllerInfo);
                                }
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    });
                }

                using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.index.html"))
                {
                    var bs = new byte[ms.Length];
                    ms.Read(bs, 0, bs.Length);
                    var text = Encoding.UTF8.GetString(bs).Replace("$$Controllers$$", controllerInfos.OrderBy(m => m.desc).ToJsonString()).Replace("$$Types$$", "[]");
                    client.OutputHttp200(text);
                }

                return true;
            }
            return false;
        }
    }
}
