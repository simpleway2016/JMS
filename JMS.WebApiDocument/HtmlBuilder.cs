using JMS.AssemblyDocumentReader;
using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Way.Lib;

namespace JMS.WebApiDocument
{
    public class HtmlBuilder
    {
        public static Task Build(HttpContext context, List<Type> controllerTypes)
        {
            List<ControllerInfo> controllerInfos = new List<ControllerInfo>();
            List<DataTypeInfo> dataTypeInfos = new List<DataTypeInfo>();
            foreach (var controllerType in controllerTypes)
            {
                Build(context, dataTypeInfos, controllerInfos, controllerType);
            }

            if(ServiceRedirects.Configs != null)
            {
                foreach( var config in ServiceRedirects.Configs)
                {
                    using ( var client = ServiceRedirects.ClientProviderFunc())
                    {
                        var service = client.TryGetMicroService(config.ServiceName);
                        if (service != null)
                        {
                            try
                            {
                                var jsonContent = service.GetServiceInfo();
                                var controllerInfo = jsonContent.FromJson<ControllerInfo>();
                                controllerInfo.desc = config.Description;
                                foreach( var method in controllerInfo.items)
                                {                                   
                                    method.url = $"/JMSRedirect/{HttpUtility.UrlEncode(config.ServiceName)}/{method.title}";
                                }
                                controllerInfo.buttons = config.Buttons?.Select(m=>new ButtonInfo { 
                                    name = m.Name,
                                    url = m.Url
                                }).ToList();
                                controllerInfos.Add(controllerInfo);
                            }
                            catch (Exception ex)
                            {
                                context.RequestServices.GetService<ILogger<JMS.WebApiDocument.HtmlBuilder>>()?.LogError(ex, "");
                            }
                        }
                        else
                        {
                            context.RequestServices.GetService<ILogger<JMS.WebApiDocument.HtmlBuilder>>()?.LogInformation($"没有在网关中获取到微服务:{config.ServiceName}");
                        }
                    }
                }
            }

            using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.index.html"))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                var bs = new byte[ms.Length];
                ms.Read(bs, 0, bs.Length);
                var text = Encoding.UTF8.GetString(bs).Replace("$$Controllers$$", controllerInfos.OrderBy(m=>m.desc).ToJsonString()).Replace("$$Types$$", dataTypeInfos.ToJsonString());
                return context.Response.WriteAsync(text);
            }
        }

        static void Build(HttpContext context, List<DataTypeInfo> dataTypeInfos , List<ControllerInfo> controllerInfos, Type controllerType)
        {
            WebApiDocAttribute attr = controllerType.GetCustomAttribute<WebApiDocAttribute>();
            var btnAttrs = controllerType.GetCustomAttributes<WebApiDocButtonAttribute>();

           

            var route = controllerType.GetCustomAttribute<RouteAttribute>();
            ControllerInfo controllerInfo = new ControllerInfo();
            controllerInfos.Add(controllerInfo);

            if (btnAttrs.Count() > 0)
            {
                controllerInfo.buttons = new List<ButtonInfo>();
                foreach( var btnattr in btnAttrs)
                {
                    controllerInfo.buttons.Add(new ButtonInfo() { 
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
            

         
            if(attr.MicroServiceType != null)
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
                    minfo.method = httpPostAttr != null ? "POST" :"GET";
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
                return getType(dataTypeInfos , type) + "[]";
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

            if (dataTypeInfos.Any(m=>m.type == type))
            {
                return "#" + dataTypeInfos.FirstOrDefault(m => m.type == type).typeName;
            }


            if (type == typeof(int))
                return "number";
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

                for(int i = 0; i < names.Length; i ++)
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
        static string GetPropertyComment(Type type,PropertyInfo pro)
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

        static string GetParameterComment(Type type, MethodInfo method,ParameterInfo parameter)
        {
            try
            {
                var typeDoc = DocumentReader.GetTypeDocument(type);
                return typeDoc.Methods.FirstOrDefault(m => m.Name == method.Name).Parameters.FirstOrDefault(m=>m.Name == parameter.Name).Comment;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
