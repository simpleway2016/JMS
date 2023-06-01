using JMS.Domains;
using JMS.Dtos;
using JMS.ServerCore.Http;
using JMS.WebApiDocument;
using JMS.WebApiDocument.Dtos;
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
        IRegisterServiceManager _registerServiceManager;
        public JmsDocMiddleware(IRegisterServiceManager registerServiceManager)
        {
            this._registerServiceManager = registerServiceManager;

        }
        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, IDictionary<string, string> headers)
        {
            if (requestPath.StartsWith("/JmsDoc", StringComparison.OrdinalIgnoreCase))
            {
                if (_registerServiceManager.SupportJmsDoc == false)
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
                var serviceInfos = _registerServiceManager.GetAllRegisterServices().ToArray();
                foreach (var serviceInfo in serviceInfos)
                {
                    foreach (var serviceItem in serviceInfo.ServiceList)
                    {
                        if (serviceItem.AllowGatewayProxy == false || controllerInfos.Any(m => m.name == serviceItem.Name))
                            continue;
                        try
                        {
                            using (var proxyRemoteClient = new RemoteClient(new[] { new NetAddress("127.0.0.1", ((IPEndPoint)client.Socket.LocalEndPoint).Port) }))
                            {
                                var location = new ClientServiceDetail(serviceItem, serviceInfo);

                                var service = proxyRemoteClient.GetMicroService(serviceItem.Name, location);
                                if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.JmsService)
                                {
                                    var jsonContent = service.GetServiceInfo();
                                    var controllerInfo = jsonContent.FromJson<ControllerInfo>();
                                    controllerInfo.name = serviceItem.Name;
                                    controllerInfo.desc = string.IsNullOrWhiteSpace(serviceItem.Description) ? serviceItem.Name : serviceItem.Description;
                                    foreach (var method in controllerInfo.items)
                                    {
                                        method.url = $"/{HttpUtility.UrlEncode(serviceItem.Name)}/{method.title}";
                                    }
                                    if (controllerInfo.items.Count == 1)
                                        controllerInfo.items[0].opened = true;
                                    controllerInfos.Add(controllerInfo);
                                }
                                else if (service.ServiceLocation.Type == JMS.Dtos.ServiceType.WebSocket)
                                {
                                    var jsonContent = service.GetServiceInfo();
                                    var serviceinfo = jsonContent.FromJson<ControllerInfo>();
                                    var controllerInfo = new ControllerInfo()
                                    {
                                        name = serviceItem.Name,
                                        desc = string.IsNullOrWhiteSpace(serviceItem.Description) ? serviceItem.Name : serviceItem.Description,
                                    };
                                    controllerInfo.items = new List<MethodItemInfo>();
                                    controllerInfo.items.Add(new MethodItemInfo
                                    {
                                        title = "WebSocket接口",
                                        method = serviceinfo.desc,
                                        isComment = true,
                                        isWebSocket = true,
                                        opened = true,
                                        url = $"/{HttpUtility.UrlEncode(serviceItem.Name)}"
                                    });
                                    controllerInfos.Add(controllerInfo);
                                }
                            }
                        }
                        catch (Exception)
                        {
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
