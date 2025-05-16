using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace JMS.WebApiDocument.Dtos
{
    public class ControllerInfo
    {
        public string name { get; set; }
        public bool isPrivate { get; set; }
        public string desc { get; set; }
        public List<MethodItemInfo> items { get; set; }
        public List<ButtonInfo> buttons { get; set; }
        public List<DataTypeInfo> dataTypeInfos { get; set; }

        public static void FormatForBuildCode(ControllerInfo controllerInfo,string serviceName)
        {
            foreach (var method in controllerInfo.items)
            {
                method.parameters = method.data.items;
                method.data = null;
                method.url = $"/{HttpUtility.UrlEncode(serviceName)}/{method.title}";
                if (method.returnData == null)
                {
                    method.returnData = new DataBodyInfo { type = "void" };
                }
                else
                {
                    if (method.returnData.type.StartsWith("#"))
                    {
                        method.returnData.type = method.returnData.type.Substring(1);

                        int index;
                        if ((index = method.returnData.type.LastIndexOf(".")) >= 0)
                        {
                            method.returnData.type = method.returnData.type.Substring(index + 1);
                        }
                        method.returnData.type = $"{serviceName}_{method.returnData.type}";
                    }
                }
            }

            foreach (var typeInfo in controllerInfo.dataTypeInfos)
            {
                int index;
                if ((index = typeInfo.typeName.LastIndexOf(".")) >= 0)
                {
                    typeInfo.typeName = typeInfo.typeName.Substring(index + 1);
                }
                typeInfo.typeName = $"{serviceName}_{typeInfo.typeName}";
            }
        }
    }

    public class ButtonInfo
    {
        public string name { get; set; }
        public string url { get; set; }
    }

    public class DataTypeInfo
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public Type type { get; set; }
        public string typeName { get; set; }
        public bool isEnum { get; set; }
        public List<ParameterInformation> members { get; set; }

        public DataTypeInfo(Type type)
        {
            this.type = type;
        }
    }

    public class MethodItemInfo
    {
        public bool isComment { get; set; }
        public string title { get; set; }
        public string titleGroup { get; set; }
        public string method { get; set; }
        public string desc { get; set; }
        public string url { get; set; }
        public bool opened { get; set; }
        public bool isWebSocket { get; set; }

        public List<ParameterInformation> query { get; set; }
        public List<ParameterInformation> form { get; set; }
        public List<ParameterInformation> parameters { get; set; }
        public DataBodyInfo data { get; set; }
        public DataBodyInfo returnData { get; set; }

    }
    public class ParameterInformation
    {
        public string name { get; set; }
        public string desc { get; set; }
        public string type { get; set; }
        public bool isNullable { get; set; }
    }
    public class DataBodyInfo
    {
        public string type { get; set; }
        public string desc { get; set; }
        public List<ParameterInformation> items { get; set; }

    }
}
