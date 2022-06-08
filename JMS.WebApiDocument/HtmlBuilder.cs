using JMS.WebApiDocument.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Way.Lib;

namespace JMS.WebApiDocument
{
    public class HtmlBuilder
    {
        public static Task Build(HttpContext context, List<Type> controllerTypes, XElement membersEle)
        {
            List<ControllerInfo> controllerInfos = new List<ControllerInfo>();
            List<DataTypeInfo> dataTypeInfos = new List<DataTypeInfo>();
            foreach (var controllerType in controllerTypes)
            {
                Build(context, dataTypeInfos, controllerInfos, controllerType, membersEle);
            }

            using (var ms = typeof(HtmlBuilder).Assembly.GetManifestResourceStream("JMS.WebApiDocument.index.html"))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                var bs = new byte[ms.Length];
                ms.Read(bs, 0, bs.Length);
                var text = Encoding.UTF8.GetString(bs).Replace("$$Controllers$$", controllerInfos.ToJsonString()).Replace("$$Types$$", dataTypeInfos.ToJsonString());
                return context.Response.WriteAsync(text);
            }
        }

        static void Build(HttpContext context, List<DataTypeInfo> dataTypeInfos , List<ControllerInfo> controllerInfos, Type controllerType, XElement membersEle)
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
                minfo.desc = GetMethodComment(attr.MicroServiceType, method, membersEle);
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
                    pinfo.desc = GetParameterComment(attr.MicroServiceType, method, param, membersEle);
                    pinfo.type = getType(dataTypeInfos , param.ParameterType, membersEle);
                }

                if (method.ReturnType != typeof(void))
                {
                    minfo.returnData = new DataBodyInfo();
                    minfo.returnData.type = getType(dataTypeInfos, method.ReturnType, membersEle);
                    if( minfo.returnData.type.EndsWith("[]") == false)
                    {
                        var typeinfo = dataTypeInfos.FirstOrDefault(m => m.type == method.ReturnType);
                        if(typeinfo != null)
                        {
                            minfo.returnData.items = typeinfo.members;
                        }
                    }
                    minfo.returnData.desc = GetMethodReturnComment(attr.MicroServiceType, method, membersEle);
                }
            }
        }

        static string getType(List<DataTypeInfo> dataTypeInfos, Type type, XElement membersEle)
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
                return getType(dataTypeInfos , type, membersEle) + "[]";
            }
            else if (type.IsArray == false && type.GetInterfaces().Any(m => m == typeof(System.Collections.IList)))
            {
                return "any[]";
            }
            else if (type.IsArray == true)
            {
                type = type.GetElementType();
                return getType(dataTypeInfos, type, membersEle) + "[]";
            }

            if (dataTypeInfos.Any(m=>m.type == type))
            {
                return "#" + dataTypeInfos.FirstOrDefault(m => m.type == type).typeName;
            }


            if (type == typeof(string))
                return "String";
            else if (type.IsArray == false && type.IsValueType == false && type != typeof(string))
            {
                DataTypeInfo dataTypeInfo = new DataTypeInfo(type);
                dataTypeInfo.typeName = type.FullName;
                dataTypeInfo.members = new List<ParameterInformation>();
                dataTypeInfos.Add(dataTypeInfo);

                var properties = type.GetProperties();
                foreach (var pro in properties)
                {
                    var pinfo = new ParameterInformation();
                    dataTypeInfo.members.Add(pinfo);
                    pinfo.name = pro.Name;
                    pinfo.desc = GetPropertyComment(type, pro, membersEle);
                    pinfo.type = getType(dataTypeInfos, pro.PropertyType, membersEle);
                }
                return "#" + dataTypeInfo.typeName;
            }
            else if (type.IsEnum)
            {
                var names = Enum.GetNames(type);
                var values = Enum.GetValues(type);

                DataTypeInfo dataTypeInfo = new DataTypeInfo(type);
                dataTypeInfo.typeName = type.FullName;
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
                    pinfo.desc = GetEnumFieldComment(type, names[i], membersEle);
                    pinfo.type = "";
                }
                return "#" + dataTypeInfo.typeName;
            }
            else if (type.IsValueType)
                return "any";

            return "any";
        }

        static string GetEnumFieldComment(Type type, string name, XElement membersEle)
        {
            var ele = membersEle.Elements("member").FirstOrDefault(m => m.Attribute("name").Value == $"F:{type.FullName.Replace("+", ".")}.{name}");
            try
            {
                if (ele != null)
                {
                    var commentEle = ele.Element("summary");
                    return commentEle.Value.Trim();
                }
            }
            catch
            {

            }
            return "";
        }
        //
        static string GetMethodComment(Type type, MethodInfo method, XElement membersEle)
        {
            try
            {
                var commentEle = membersEle.Elements("member").Where(m =>
                              m.Attribute("name").Value == $"M:{type.FullName.Replace("+", ".")}.{method.Name}" ||
                              m.Attribute("name").Value.StartsWith($"M:{type.FullName.Replace("+", ".")}.{method.Name}<") ||
                              m.Attribute("name").Value.StartsWith($"M:{type.FullName.Replace("+", ".")}.{method.Name}(")).FirstOrDefault();

                string methodComment = commentEle?.Element("summary").Value.Trim();
                return methodComment;
            }
            catch (Exception)
            {
                return "";
            }

        }
        static string GetMethodReturnComment(Type type, MethodInfo method, XElement membersEle)
        {
            try
            {
                var commentEle = membersEle.Elements("member").Where(m =>
                              m.Attribute("name").Value == $"M:{type.FullName.Replace("+", ".")}.{method.Name}" ||
                              m.Attribute("name").Value.StartsWith($"M:{type.FullName.Replace("+", ".")}.{method.Name}<") ||
                              m.Attribute("name").Value.StartsWith($"M:{type.FullName.Replace("+", ".")}.{method.Name}(")).FirstOrDefault();

                string methodComment = commentEle?.Element("returns").Value.Trim();
                return methodComment;
            }
            catch (Exception)
            {
                return "";
            }

        }
        static string GetPropertyComment(Type type,PropertyInfo pro,XElement membersEle)
        {
            var ele = membersEle.Elements("member").FirstOrDefault(m => m.Attribute("name").Value == $"P:{ pro.DeclaringType.FullName.Replace("+", ".")}.{pro.Name}");
            try
            {
                if (ele != null)
                {
                    var commentEle = ele.Element("summary");
                    return commentEle.Value.Trim();
                }
            }
            catch
            {

            }
            return "";
        }

        static string GetParameterComment(Type type, MethodInfo method,ParameterInfo parameter, XElement membersEle)
        {
            try
            {
                var commentEle = membersEle.Elements("member").Where(m =>
                            m.Attribute("name").Value == $"M:{type.FullName.Replace("+", ".")}.{method.Name}" ||
                            m.Attribute("name").Value.StartsWith($"M:{type.FullName.Replace("+", ".")}.{method.Name}<") ||
                            m.Attribute("name").Value.StartsWith($"M:{type.FullName.Replace("+", ".")}.{method.Name}(")).FirstOrDefault();
                var pele = commentEle?.Elements("param").FirstOrDefault(m => m.Attribute("name").Value == parameter.Name);
                var comment = pele?.Value.Trim();
                if (parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(System.Nullable<>))
                {
                    comment += " （可为null）";
                }
                return comment;
            }
            catch (Exception ex)
            {
                return "";
            }
        }
    }
}
