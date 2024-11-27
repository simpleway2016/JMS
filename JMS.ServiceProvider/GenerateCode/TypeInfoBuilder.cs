using JMS.AssemblyDocumentReader;
using JMS.Controllers;
using JMS.ServerCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace JMS.GenerateCode
{
    public class TypeInfoBuilder
    {
        public static ControllerInfo Build(ControllerTypeInfo controllerTypeInfo)
        {
            var controllerType = controllerTypeInfo.Type;
            ControllerInfo controllerInfo = new ControllerInfo()
            {
                name = controllerTypeInfo.Service.Name,
                desc = GetTypeComment(controllerType),
                items = new List<MethodItemInfo>(),
                dataTypeInfos = new List<DataTypeInfo>()
            };
            var dataTypeInfos = controllerInfo.dataTypeInfos;
            var methods = controllerTypeInfo.Methods.Select(m => m.Method).ToArray();
            foreach (var method in methods)
            {
                MethodItemInfo minfo = new MethodItemInfo();
                controllerInfo.items.Add(minfo);
                minfo.title = method.Name;

                var categoryAttr = method.GetCustomAttribute(typeof(CategoryAttribute)) as CategoryAttribute;
                if(categoryAttr != null)
                {
                    minfo.titleGroup = categoryAttr.Category;
                }
                minfo.desc = GetMethodComment(controllerType, method);
                minfo.method = "POST";

                var parameters = method.GetParameters();
                minfo.data = new DataBodyInfo();
                minfo.data.type = "Array";
                minfo.data.items = new List<ParameterInformation>();

                foreach (var param in parameters)
                {
                    var pinfo = new ParameterInformation();
                    minfo.data.items.Add(pinfo);
                    pinfo.name = param.Name;
                    pinfo.desc = GetParameterComment(controllerType, method, param);
                    pinfo.type = getType(dataTypeInfos, param.ParameterType, controllerTypeInfo.Service.AllowGatewayProxy == false);
                    if (param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == typeof(System.Nullable<>))
                    {
                        //{Name = "NullableAttribute" FullName = "System.Runtime.CompilerServices.NullableAttribute"}
                        pinfo.isNullable = true;
                    }
                    else if(param.ParameterType == typeof(string))
                    {
                        var attArr = param.GetCustomAttributes().ToArray();
                        if(attArr.Any(m=>m.GetType().Name == "NullableAttribute"))
                        {
                            pinfo.isNullable = true;
                            pinfo.type += "?";
                        }
                    }
                }

                var returnType = GetReturnType(method.ReturnType);
                if (returnType != typeof(void))
                {
                    minfo.returnData = new DataBodyInfo();
                    minfo.returnData.type = getType(dataTypeInfos, returnType,controllerTypeInfo.Service.AllowGatewayProxy == false);
                    if (minfo.returnData.type.EndsWith("[]") == false)
                    {
                        var typeinfo = dataTypeInfos.FirstOrDefault(m => m.type == returnType);
                        if (typeinfo != null)
                        {
                            minfo.returnData.items = typeinfo.members;
                        }
                    }
                    minfo.returnData.desc = GetMethodReturnComment(controllerType, method);
                }
            }

            controllerInfo.items = controllerInfo.items.OrderBy(m => m.titleGroup).ThenBy(m => m.title).ToList();
            return controllerInfo;
        }

        public static Type GetReturnType(Type type,bool isNullable =false)
        {
            if (type == typeof(HttpResult) )
            {
                return typeof(object);
            }
            else if (type == typeof(Task) || type == typeof(ValueTask))
            {
                return typeof(void);
            }
            else if (type.IsSubclassOf(typeof(Task)) && type.IsGenericType)
            {
                Type[] argTypes = type.GetGenericArguments();
                return GetReturnType(argTypes[0]);
            }
            else if (type.IsSubclassOf(typeof(ValueTask)) && type.IsGenericType)
            {
                Type[] argTypes = type.GetGenericArguments();
                return GetReturnType(argTypes[0]);
            }
            return type;
        }

        static string getType(List<DataTypeInfo> dataTypeInfos, Type type,bool isPrivagte)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Nullable<>))
            {
                type = type.GenericTypeArguments[0];
                return getType(dataTypeInfos, type, isPrivagte) + "?";
            }

            if (type == typeof(object))
            {
                return isPrivagte ? "object" : "any";
            }
            else if (type.IsArray == false && type.GetInterfaces().Any(m => m.Name == "IDictionary"))
            {
                return isPrivagte ? "IDictionary" : "any";
            }
            else if (type.IsArray == false && type.IsGenericType && type.GetInterfaces().Any(m => m == typeof(System.Collections.IDictionary)))
            {
                return isPrivagte ? "IDictionary" : "any";
            }
            else if (type.IsArray == false && type.IsGenericType && type.GetInterfaces().Any(m => m == typeof(System.Collections.IList)))
            {
                type = type.GenericTypeArguments[0];
                return getType(dataTypeInfos, type, isPrivagte) + "[]";
            }
            else if (type.IsArray == false && type.IsGenericType && type.GetInterfaces().Any(m => m == typeof(System.Collections.IEnumerable)))
            {
                type = type.GenericTypeArguments[0];
                return getType(dataTypeInfos, type, isPrivagte) + "[]";
            }
            else if (type.IsArray == false && type.GetInterfaces().Any(m => m == typeof(System.Collections.IList)))
            {
                return "any[]";
            }
            else if (type.IsArray == true)
            {
                type = type.GetElementType();
                return getType(dataTypeInfos, type, isPrivagte) + "[]";
            }

            if (dataTypeInfos.Any(m => m.type == type))
            {
                return "#" + dataTypeInfos.FirstOrDefault(m => m.type == type).typeName;
            }

            if (isPrivagte)
            {
                if (type == typeof(int))
                    return "int";
                else if (type == typeof(uint))
                    return "uint";
                else if (type == typeof(long))
                    return "long";
                else if (type == typeof(ulong))
                    return "ulong";
                else if (type == typeof(short))
                    return "short";
                else if (type == typeof(ushort))
                    return "ushort";
                else if (type == typeof(float))
                    return "float";
                else if (type == typeof(double))
                    return "double";
                else if (type == typeof(decimal))
                    return "decimal";
                else if (type == typeof(bool))
                    return "bool";
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
                        pinfo.type = getType(dataTypeInfos, pro.PropertyType, isPrivagte);
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
                    dataTypeInfo.isEnum = true;
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
                        pinfo.type = names[i];
                    }
                    return "#" + dataTypeInfo.typeName;
                }

                return type.Name;
            }
            else
            {
                if (type == typeof(int))
                    return "number";
                else if (type == typeof(uint))
                    return "number";
                else if (type == typeof(long))
                    return "number";
                else if (type == typeof(ulong))
                    return "number";
                else if (type == typeof(short))
                    return "number";
                else if (type == typeof(ushort))
                    return "number";
                else if (type == typeof(float))
                    return "number";
                else if (type == typeof(double))
                    return "number";
                else if (type == typeof(decimal))
                    return "number";
                else if (type == typeof(bool))
                    return "boolean";
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
                        pinfo.type = getType(dataTypeInfos, pro.PropertyType, isPrivagte);
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
                    dataTypeInfo.isEnum = true;
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
                        pinfo.type = names[i];
                    }
                    return "#" + dataTypeInfo.typeName;
                }

                return "any";
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

        static string GetTypeComment(Type type)
        {
            try
            {
                var typeDoc = DocumentReader.GetTypeDocument(type);
                return typeDoc.Comment;
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
