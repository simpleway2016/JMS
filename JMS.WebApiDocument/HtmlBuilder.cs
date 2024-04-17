using JMS.AssemblyDocumentReader;
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
            var index = requestPath.IndexOf("/JmsDoc/OutputCode/" ,  StringComparison.OrdinalIgnoreCase);
            var servicename = requestPath.Substring(index + 19);

            var buttonName = context.Request.Query["button"].ToString();

            using (var rc = ServiceRedirects.ClientProviderFunc())
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


                        context.Response.ContentType = "text/html; charset=utf-8";
                        await context.Response.WriteAsync(text);
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
            using (var client = ServiceRedirects.ClientProviderFunc())
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

                foreach( var serviceInfo in doneList)
                {
                    var service = await client.TryGetMicroServiceAsync(serviceInfo.Name);
                    if (service != null)
                        services.Add(service);
                }

                ConcurrentQueue<string> pendingOutputs = new ConcurrentQueue<string>();

                await Parallel.ForEachAsync(services, async (service, cancelToken) => {
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
                            var outputContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(controllerInfo.ToJsonString()));
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

        public static async Task Build(HttpContext context, List<Type> controllerTypes)
        {
            using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.index.html"))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                var bs = new byte[ms.Length];
                ms.Read(bs, 0, bs.Length);
                var text = Encoding.UTF8.GetString(bs);
                await context.Response.WriteAsync(text);
            }
        }

        static void Build(HttpContext context, List<DataTypeInfo> dataTypeInfos, List<ControllerInfo> controllerInfos, Type controllerType)
        {
            WebApiDocAttribute attr = controllerType.GetCustomAttribute<WebApiDocAttribute>();
            var btnAttrs = controllerType.GetCustomAttributes<WebApiDocButtonAttribute>();



            var route = controllerType.GetCustomAttribute<RouteAttribute>();
            ControllerInfo controllerInfo = new ControllerInfo();
            controllerInfos.Add(controllerInfo);

            if (btnAttrs.Count() > 0)
            {
                controllerInfo.buttons = new List<ButtonInfo>();
                foreach (var btnattr in btnAttrs)
                {
                    controllerInfo.buttons.Add(new ButtonInfo()
                    {
                        name = btnattr.Name,
                        url = btnattr.Url,
                    });
                }
            }

            controllerInfo.items = new List<MethodItemInfo>();
            controllerInfo.name = controllerType.Name;
            if (controllerInfo.name.EndsWith("Controller"))
                controllerInfo.name = controllerInfo.name.Substring(0, controllerInfo.name.Length - "Controller".Length);
            controllerInfo.desc = attr.Description;



            if (attr.MicroServiceType != null)
            {

                var methods = attr.MicroServiceType.GetMethods().Where(m => m.IsSpecialName == false && m.DeclaringType == attr.MicroServiceType).ToArray();
                foreach (var method in methods)
                {
                    if (method.ReturnType == typeof(Task))
                        continue;
                    if (method.ReturnType.IsGenericType && method.ReturnType.BaseType == typeof(Task))
                        continue;

                    MethodItemInfo minfo = new MethodItemInfo();
                    controllerInfo.items.Add(minfo);
                    minfo.isComment = method.GetCustomAttribute<IsCommentAttribute>() != null;
                    minfo.title = method.Name;
                    minfo.desc = GetMethodComment(attr.MicroServiceType, method);
                    minfo.method = "POST";
                    minfo.url = route.Template.Replace("[controller]", controllerInfo.name).Replace("{method}", method.Name);

                    var parameters = method.GetParameters();
                    minfo.data = new DataBodyInfo();
                    minfo.data.type = "Array";
                    minfo.data.items = new List<ParameterInformation>();

                    foreach (var param in parameters)
                    {
                        var pinfo = new ParameterInformation();
                        minfo.data.items.Add(pinfo);
                        pinfo.name = param.Name;
                        pinfo.desc = GetParameterComment(attr.MicroServiceType, method, param);
                        pinfo.type = getType(dataTypeInfos, param.ParameterType);
                    }

                    if (method.ReturnType != typeof(void))
                    {
                        minfo.returnData = new DataBodyInfo();
                        minfo.returnData.type = getType(dataTypeInfos, method.ReturnType);
                        if (minfo.returnData.type.EndsWith("[]") == false)
                        {
                            var typeinfo = dataTypeInfos.FirstOrDefault(m => m.type == method.ReturnType);
                            if (typeinfo != null)
                            {
                                minfo.returnData.items = typeinfo.members;
                            }
                        }
                        minfo.returnData.desc = GetMethodReturnComment(attr.MicroServiceType, method);
                    }
                }
            }
            else
            {
                var methods = controllerType.GetMethods().Where(m => m.IsSpecialName == false && m.DeclaringType == controllerType).ToArray();
                foreach (var method in methods)
                {
                    var httpGetAttr = method.GetCustomAttribute<HttpGetAttribute>();
                    var httpPostAttr = method.GetCustomAttribute<HttpPostAttribute>();

                    if (httpGetAttr == null && httpPostAttr == null)
                        httpGetAttr = new HttpGetAttribute();

                    MethodItemInfo minfo = new MethodItemInfo();
                    controllerInfo.items.Add(minfo);
                    minfo.isComment = method.GetCustomAttribute<IsCommentAttribute>() != null;
                    minfo.title = method.Name;
                    minfo.desc = GetMethodComment(controllerType, method);
                    minfo.method = httpPostAttr != null ? "POST" : "GET";
                    minfo.url = route.Template.Replace("[controller]", controllerInfo.name).Replace("[action]", method.Name);

                    minfo.query = new List<ParameterInformation>();
                    minfo.form = new List<ParameterInformation>();

                    var parameters = method.GetParameters();
                    minfo.data = new DataBodyInfo();
                    minfo.data.type = "Object";
                    minfo.data.items = new List<ParameterInformation>();

                    foreach (var param in parameters)
                    {
                        var fromQueryAttr = param.GetCustomAttribute<FromQueryAttribute>();
                        var fromFromAttr = param.GetCustomAttribute<FromFormAttribute>();
                        var fromBodyAttr = param.GetCustomAttribute<FromBodyAttribute>();

                        var pinfo = new ParameterInformation();
                        pinfo.name = param.Name;
                        pinfo.desc = GetParameterComment(controllerType, method, param);
                        pinfo.type = getType(dataTypeInfos, param.ParameterType);
                        if (param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == typeof(System.Nullable<>))
                        {
                            pinfo.isNullable = true;
                        }
                        if (fromQueryAttr != null || (fromQueryAttr == null && fromFromAttr == null && fromBodyAttr == null))
                        {
                            minfo.query.Add(pinfo);
                        }
                        else if (fromFromAttr != null)
                        {
                            minfo.form.Add(pinfo);
                        }
                        else if (fromBodyAttr != null)
                        {
                            var typeinfo = dataTypeInfos.FirstOrDefault(m => m.type == param.ParameterType);
                            if (typeinfo != null)
                                minfo.data.items = typeinfo.members;
                        }
                    }

                    if (method.ReturnType != typeof(void))
                    {
                        minfo.returnData = new DataBodyInfo();
                        minfo.returnData.type = getType(dataTypeInfos, method.ReturnType);
                        if (minfo.returnData.type.EndsWith("[]") == false)
                        {
                            var typeinfo = dataTypeInfos.FirstOrDefault(m => m.type == method.ReturnType);
                            if (typeinfo != null)
                            {
                                minfo.returnData.items = typeinfo.members;
                            }
                        }
                        minfo.returnData.desc = GetMethodReturnComment(controllerType, method);
                    }
                }
            }
        }
        static string GetFullName(List<DataTypeInfo> dataTypeInfos, Type type)
        {
            var fullname = type.FullName;
            var index = fullname.IndexOf("`");
            if (index > 0)
            {
                fullname = fullname.Substring(0, index);
            }

            string ret = fullname;
            index = 1;
            while (dataTypeInfos.Any(m => m.typeName == ret && m.type != type))
            {
                ret = fullname + index;
                index++;
            }

            return ret;
        }
        static string getType(List<DataTypeInfo> dataTypeInfos, Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Nullable<>))
            {
                type = type.GenericTypeArguments[0];
                return getType(dataTypeInfos, type);
            }

            if (type == typeof(object))
            {
                return "any";
            }
            else if (type.IsArray == false && type.GetInterfaces().Any(m => m.Name == "IDictionary"))
            {
                return "any";
            }
            else if (type.IsArray == false && type.IsGenericType && type.GetInterfaces().Any(m => m == typeof(System.Collections.IList)))
            {
                type = type.GenericTypeArguments[0];
                return getType(dataTypeInfos, type) + "[]";
            }
            else if (type.IsArray == false && type.IsGenericType && type.GetInterfaces().Any(m => m == typeof(System.Collections.IEnumerable)))
            {
                type = type.GenericTypeArguments[0];
                return getType(dataTypeInfos, type) + "[]";
            }
            else if (type.IsArray == false && type.GetInterfaces().Any(m => m == typeof(System.Collections.IList)))
            {
                return "any[]";
            }
            else if (type.IsArray == true)
            {
                type = type.GetElementType();
                return getType(dataTypeInfos, type) + "[]";
            }

            if (dataTypeInfos.Any(m => m.type == type))
            {
                return "#" + dataTypeInfos.FirstOrDefault(m => m.type == type).typeName;
            }


            if (type == typeof(int))
                return "number";
            else if (type == typeof(bool))
                return "boolean";
            else if (type == typeof(long))
                return "number";
            else if (type == typeof(short))
                return "number";
            else if (type == typeof(float))
                return "number";
            else if (type == typeof(double))
                return "number";
            else if (type == typeof(decimal))
                return "number";
            else if (type == typeof(string))
                return "string";
            else if (type.IsArray == false && type.IsValueType == false && type != typeof(string))
            {
                DataTypeInfo dataTypeInfo = new DataTypeInfo(type);
                dataTypeInfo.typeName = GetFullName(dataTypeInfos, type);
                dataTypeInfo.members = new List<ParameterInformation>();
                dataTypeInfos.Add(dataTypeInfo);

                var properties = type.GetProperties();
                foreach (var pro in properties)
                {
                    var pinfo = new ParameterInformation();
                    dataTypeInfo.members.Add(pinfo);
                    pinfo.name = pro.Name;
                    pinfo.desc = GetPropertyComment(type, pro);
                    pinfo.type = getType(dataTypeInfos, pro.PropertyType);
                }
                return "#" + dataTypeInfo.typeName;
            }
            else if (type.IsEnum)
            {
                var names = Enum.GetNames(type);
                var values = Enum.GetValues(type);

                DataTypeInfo dataTypeInfo = new DataTypeInfo(type);
                dataTypeInfo.typeName = GetFullName(dataTypeInfos, type);
                dataTypeInfo.members = new List<ParameterInformation>();
                dataTypeInfos.Add(dataTypeInfo);

                for (int i = 0; i < names.Length; i++)
                {
                    var pinfo = new ParameterInformation();
                    dataTypeInfo.members.Add(pinfo);
                    try
                    {
                        pinfo.name = ((int)values.GetValue(i)).ToString();
                    }
                    catch
                    {
                        pinfo.name = values.GetValue(i)?.ToString();

                    }
                    pinfo.desc = GetEnumFieldComment(type, names[i]);
                    pinfo.type = "";
                }
                return "#" + dataTypeInfo.typeName;
            }
            else if (type.IsValueType)
                return "any";

            return "any";
        }

        static string GetEnumFieldComment(Type type, string name)
        {
            try
            {
                var typeDoc = DocumentReader.GetTypeDocument(type);
                return typeDoc.Fields.FirstOrDefault(m => m.Name == name).Comment;
            }
            catch (Exception)
            {
                return "";
            }

        }
        //
        static string GetMethodComment(Type type, MethodInfo method)
        {
            try
            {
                var typeDoc = DocumentReader.GetTypeDocument(type);
                return typeDoc.Methods.FirstOrDefault(m => m.Name == method.Name).Comment;
            }
            catch (Exception)
            {
                return "";
            }

        }
        static string GetMethodReturnComment(Type type, MethodInfo method)
        {
            try
            {
                var typeDoc = DocumentReader.GetTypeDocument(type);
                return typeDoc.Methods.FirstOrDefault(m => m.Name == method.Name).ReturnComment;
            }
            catch (Exception)
            {
                return "";
            }

        }
        static string GetPropertyComment(Type type, PropertyInfo pro)
        {
            try
            {
                var typeDoc = DocumentReader.GetTypeDocument(type);
                return typeDoc.Properties.FirstOrDefault(m => m.Name == pro.Name).Comment;
            }
            catch (Exception)
            {
                return "";
            }
        }

        static string GetParameterComment(Type type, MethodInfo method, ParameterInfo parameter)
        {
            try
            {
                var typeDoc = DocumentReader.GetTypeDocument(type);
                return typeDoc.Methods.FirstOrDefault(m => m.Name == method.Name).Parameters.FirstOrDefault(m => m.Name == parameter.Name).Comment;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
