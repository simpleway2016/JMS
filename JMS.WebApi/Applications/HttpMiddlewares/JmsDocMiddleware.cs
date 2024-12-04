using JMS.Common;
using JMS.Dtos;
using JMS.ServerCore.Http;
using JMS.WebApiDocument;
using JMS.WebApiDocument.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly IWebApiHostEnvironment _webApiEnvironment;
        private readonly IConfiguration _configuration;
        ILogger _logger;
        public JmsDocMiddleware(IWebApiHostEnvironment webApiEnvironment,IConfiguration configuration,ILoggerFactory loggerFactory)
        {
            _webApiEnvironment = webApiEnvironment;
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger("JmsDoc");
        }
        async void outputCode(NetClient client, string httpMethod, string requestPath, Dictionary<string, string> headers)
        {

            var servicename = requestPath.Replace("/JmsDoc/OutputCode/", "");
            servicename = servicename.Substring(0, servicename.IndexOf("?"));

            var buttonName = requestPath.Substring(requestPath.IndexOf("?button=") + 8);
            buttonName = HttpUtility.UrlDecode(buttonName);

            using (var rc = new RemoteClient(_webApiEnvironment.Config.Current.Gateways))
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


            bool writeLogger = _logger.IsEnabled(LogLevel.Trace);

            using (var rc = new RemoteClient(_webApiEnvironment.Config.Current.Gateways))
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

                var allServiceInDoc = _configuration.GetSection("Http:AllServiceInDoc").Get<bool>();
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
                            if (writeLogger)
                            {
                                _logger.LogTrace($"Getting service info: {service.ServiceLocation.ToJsonString(true)}");
                            }
                            var jsonContent = await service.GetServiceInfoAsync();

                            if (writeLogger)
                            {
                                _logger.LogTrace($"{service.ServiceLocation.Name} ok");
                            }

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
                            if (writeLogger)
                            {
                                _logger.LogTrace($"Getting service info: {service.ServiceLocation.ToJsonString(true)}");
                            }
                            var jsonContent = await service.GetServiceInfoAsync();

                            if (writeLogger)
                            {
                                _logger.LogTrace($"{service.ServiceLocation.Name} ok");
                            }

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

                        if(controllerInfo != null)
                        {
                            lock (doneList)
                            {
                                var outputContent = Convert.ToBase64String(GZipHelper.Compress( Encoding.UTF8.GetBytes(controllerInfo.ToJsonString())));
                                var data = System.Text.Encoding.UTF8.GetBytes($"data: {outputContent}\n\n");
                                client.Write(data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (writeLogger)
                        {
                            _logger.LogError(ex, $"{service.ServiceLocation.Name} error");
                        }
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

                if (_configuration.GetSection("Http:SupportJmsDoc").Get<bool>() == false)
                {
                    client.OutputHttpNotFund();
                    return true;
                }

                if (requestPath.Contains("/jmsdoc.vue.pako.js", StringComparison.OrdinalIgnoreCase))
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
