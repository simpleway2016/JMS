using JMS.ApiDocument;
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
        IDocumentButtonProvider _documentButtonProvider;
        IRegisterServiceManager _registerServiceManager;
        public JmsDocMiddleware(IRegisterServiceManager registerServiceManager, IDocumentButtonProvider documentButtonProvider)
        {
            this._documentButtonProvider = documentButtonProvider;
            this._registerServiceManager = registerServiceManager;

        }

        void outputCode(NetClient client, string httpMethod, string requestPath, Dictionary<string, string> headers)
        {

            var servicename = requestPath.Replace("/JmsDoc/OutputCode/", "");
            servicename = servicename.Substring(0, servicename.IndexOf("?"));

            var buttonName = requestPath.Substring(requestPath.IndexOf("?button=") + 8);
            buttonName = HttpUtility.UrlDecode(buttonName);

            var serviceInfo = _registerServiceManager.GetAllRegisterServices().FirstOrDefault(m => m.ServiceList.Any(n => n.Name == servicename));
            if (serviceInfo == null)
                throw new Exception($"Service:{servicename} does not exist");
            var serviceItem = serviceInfo.ServiceList.FirstOrDefault(n => n.Name == servicename);
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

                    ControllerInfo.FormatForBuildCode(controllerInfo, serviceItem.Name);

                    using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.outputCode.html"))
                    {
                        var bs = new byte[ms.Length];
                        ms.Read(bs, 0, bs.Length);
                        var code = _documentButtonProvider.ApiDocCodeBuilders.FirstOrDefault(m => m.Name == buttonName).Code;
                        var vueMethods = _documentButtonProvider.ApiDocCodeBuilders.FirstOrDefault(m => m.Name == "vue methods")?.Code;
                        var text = Encoding.UTF8.GetString(bs).Replace("$$Controller$$", controllerInfo.ToJsonString()).Replace("$$code$$", code).Replace("$$vueMethods$$", vueMethods);
                        client.OutputHttpGzip200(text);
                    }
                }
            }


        }

        /// <summary>
        /// 通过sse协议，输出每个服务
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public async Task OutputSseData(NetClient client)
        {
            client.KeepAlive = false;

            var data = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-cache\r\nContent-Type: text/event-stream\r\nConnection: keep-alive\r\n\r\n");
            client.Write(data);

            List<ServiceDetail> doneList = new List<ServiceDetail>();
            List<IMicroService> services = new List<IMicroService>();

            using (var rc = new RemoteClient(new[] { new NetAddress("127.0.0.1", ((IPEndPoint)client.Socket.LocalEndPoint).Port) }))
            {
                var buttons = _documentButtonProvider.GetButtons();

                var allServiceInDoc = _registerServiceManager.AllServiceInDoc;
                var allServices = _registerServiceManager.GetAllRegisterServices().ToArray();

                foreach (var serviceRunningInfo in allServices)
                {
                    foreach (var serviceInfo in serviceRunningInfo.ServiceList)
                    {
                        if ((serviceInfo.AllowGatewayProxy == false && allServiceInDoc == false) || doneList.Any(m => m.Name == serviceInfo.Name))
                            continue;
                        doneList.Add(serviceInfo);


                    }
                }

                foreach (var serviceInfo in doneList)
                {
                    var service = await rc.TryGetMicroServiceAsync(serviceInfo.Name);
                    if (service != null)
                        services.Add(service);
                }

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
                            controllerInfo.name = service.ServiceLocation.Name;
                            controllerInfo.desc = string.IsNullOrWhiteSpace(service.ServiceLocation.Description) ? service.ServiceLocation.Name : service.ServiceLocation.Description;
                            controllerInfo.buttons = new List<ButtonInfo>(_documentButtonProvider.GetButtons());
                            foreach (var btn in controllerInfo.buttons)
                            {
                                btn.url += $"/JmsDoc/OutputCode/{service.ServiceLocation.Name}?button={HttpUtility.UrlEncode(btn.name)}";
                            }
                            foreach (var method in controllerInfo.items)
                            {
                                method.url = $"/{HttpUtility.UrlEncode(service.ServiceLocation.Name)}/{method.title}";
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
                                isPrivate = !service.ServiceLocation.AllowGatewayProxy,
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
                                url = $"/{HttpUtility.UrlEncode(service.ServiceLocation.Name)}"
                            });
                        }

                        if (controllerInfo != null)
                        {
                            lock (doneList)
                            {
                                var outputContent = Convert.ToBase64String(Common.GZipHelper.Compress(Encoding.UTF8.GetBytes(controllerInfo.ToJsonString())));
                                var data = System.Text.Encoding.UTF8.GetBytes($"data: {outputContent}\n\n");
                                client.Write(data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                });
            }

            data = System.Text.Encoding.UTF8.GetBytes($"data: ok\n\n");
            client.Write(data);
        }

        public async Task<bool> Handle(NetClient client, string httpMethod, string requestPath, Dictionary<string, string> headers)
        {
            if (requestPath.StartsWith("/JmsDoc", StringComparison.OrdinalIgnoreCase))
            {
                if (requestPath.StartsWith("/JmsDocSse", StringComparison.OrdinalIgnoreCase))
                {
                    await OutputSseData(client);
                    return true;
                }

                if (_registerServiceManager.SupportJmsDoc == false)
                {
                    client.OutputHttpNotFund();
                    return true;
                }
                else if (requestPath.StartsWith("/JmsDoc/OutputCode/", StringComparison.OrdinalIgnoreCase))
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
                else if (requestPath.Contains("/jmsdoc.vue.pako.js", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime dateTime = new DateTime(2023, 5, 12, 18, 53, 33);
                    if (headers.ContainsKey("If-Modified-Since") && Convert.ToDateTime(headers["If-Modified-Since"]) == dateTime)
                    {
                        client.OutputHttpCode(304, "NotModified");
                        return true;
                    }

                    string formattedDateTime = dateTime.ToUniversalTime().ToString("r");

                    using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.jmsdoc.vue.pako.js"))
                    {
                        var bs = new byte[ms.Length];
                        ms.Read(bs, 0, bs.Length);

                        client.OutputHttpGzip200(bs, "text/javascript", $"Last-Modified: {formattedDateTime}");
                    }
                    return true;
                }


                using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.index.html"))
                {
                    var bs = new byte[ms.Length];
                    ms.Read(bs, 0, bs.Length);

                    client.OutputHttpGzip200(bs);
                }

                return true;
            }
            return false;
        }
    }
}
