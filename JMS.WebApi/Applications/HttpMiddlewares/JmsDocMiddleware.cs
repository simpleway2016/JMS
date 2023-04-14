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
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IDictionary<string, string> headers)
        {
            if (requestPath.StartsWith("/JmsDoc", StringComparison.OrdinalIgnoreCase))
            {
                if (WebApiProgram.Configuration.GetSection("Http:SupportJmsDoc").Get<bool>() == false)
                {
                    client.OutputHttpNotFund();
                    return true;
                }

                if (requestPath.StartsWith("/JmsDoc/vue.js", StringComparison.OrdinalIgnoreCase))
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
                List<string> doneList = new List<string>();
                using (var rc = new RemoteClient(WebApiProgram.GatewayAddresses))
                {
                    var allServices = await rc.ListMicroServiceAsync(null);
                    foreach (var serviceRunningInfo in allServices)
                    {
                        foreach (var serviceInfo in serviceRunningInfo.ServiceList)
                        {
                            if (serviceInfo.AllowGatewayProxy == false || doneList.Contains(serviceInfo.Name))
                                continue;

                            try
                            {
                                doneList.Add(serviceInfo.Name);

                                var service = await rc.TryGetMicroServiceAsync(serviceInfo.Name);
                                if (service == null)
                                    continue;

                                if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.JmsService)
                                {
                                    var jsonContent = service.GetServiceInfo();
                                    var controllerInfo = jsonContent.FromJson<ControllerInfo>();
                                    if (!string.IsNullOrWhiteSpace(serviceInfo.Description))
                                    {
                                        controllerInfo.desc = serviceInfo.Description;
                                    }
                                    foreach (var method in controllerInfo.items)
                                    {
                                        method.url = $"/{HttpUtility.UrlEncode(serviceInfo.Name)}/{method.title}";
                                    }
                                    if (controllerInfo.items.Count == 1)
                                        controllerInfo.items[0].opened = true;

                                    controllerInfo.buttons = null;
                                    controllerInfos.Add(controllerInfo);
                                }
                                else if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.WebSocket)
                                {
                                    var jsonContent = service.GetServiceInfo();
                                    var cInfo = jsonContent.FromJson<ControllerInfo>();

                                    var controllerInfo = new ControllerInfo()
                                    {
                                        name = serviceInfo.Name,
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
                                    controllerInfos.Add(controllerInfo);
                                }
                            }
                            catch (Exception ex)
                            {

                            }
                        }
                    }
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
