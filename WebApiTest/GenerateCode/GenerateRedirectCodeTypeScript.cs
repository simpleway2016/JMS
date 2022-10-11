using JMS.AssemblyDocumentReader;
using JMS.GenerateCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace WebApiTest.GenerateCode
{
    public class GenerateRedirectCodeTypeScript
    {
        ControllerInfo  _controllerInfo;
        Dictionary<string, string> _typeNameDict = new Dictionary<string, string>();
        public GenerateRedirectCodeTypeScript(string serviceInfo)
        {
            _controllerInfo = serviceInfo.FromJson<ControllerInfo>();
        }


        public string BuildEnum(DataTypeInfo typeinfo)
        {

            var codeitem = new CodeItem($"export enum {getTypeName(typeinfo.typeName)}");

            foreach (var f in typeinfo.members)
            {
                codeitem.AddString($"/** {f.desc} */");

                codeitem.AddString($"{f.type} = { f.name }{ (typeinfo.members.LastOrDefault() == f ? "" : ",") }");
            }
            return codeitem.Build();
        }


      

        public string BuildType(DataTypeInfo typeinfo)
        {
            if (typeinfo.isEnum)
                return BuildEnum(typeinfo);

            List<string> befores = new List<string>();
            var codeitem = new CodeItem($"export interface {getTypeName(typeinfo.typeName)}");
          
            foreach (var pro in typeinfo.members)
            {

                codeitem.AddString($"/** {pro.desc} */");

                string type = getReferenceTypeName(pro.type);
                codeitem.AddString($"{pro.name}?:{type};");
            }
            StringBuilder result = new StringBuilder();
            foreach (var code in befores)
            {
                result.AppendLine(code);
            }
            result.AppendLine(codeitem.Build());
            return result.ToString();
        }

        string getTypeName(string typename)
        {
            return $"{_controllerInfo.name}Api_{typename.Split('.').Last()}";
        }

        string getReferenceTypeName(string type)
        {
            if (type.StartsWith("#"))
            {
                type = type.Substring(1);
                if (_typeNameDict.ContainsKey(type))
                    return _typeNameDict[type];

                var flag = _controllerInfo.name + "Api_" + type.Split('.').Last();
                var name = flag;

                var index = 1;
                while( _typeNameDict.Any(m=>m.Value == name))
                {
                    name = $"{flag}{index}";
                    index++;
                }
                _typeNameDict[type] = name;
                return name;
            }
            else
            {
                return type;
            }
        }

        public string Build()
        {

            List<string> methodComments = new List<string>();           

            var classCodeItem = new CodeItem($"export class {_controllerInfo.name}Api");

         

            foreach (var method in _controllerInfo.items)
            {

                string returnType;
                if (method.returnData != null)
                {
                    returnType = getReferenceTypeName( method.returnData.type);
                }
                else
                {
                    returnType = "void";
                }


                string comment = "/**";

                string parameterString = "";
                string invokeParameterStr = "";
                string methodComment = method.desc;

              
                comment += "\n* " + methodComment;
                var parameters = method.data.items;
                comment += "\n* @param component";

                methodComments.Add($"/** {method.title} {methodComment} */");

                for (int i = 0; i < parameters.Count; i++)
                {
                    var p = parameters[i];
                    if (parameterString.Length > 0)
                    {
                        parameterString += ",";
                        invokeParameterStr += ",";
                    }

                    string pname = p.name;

                    parameterString += pname + ":" + getReferenceTypeName(p.type);
                    invokeParameterStr += p.name;

                    comment += "\n* @param " + p.name + " " + p.desc?.Replace("\r", "").Replace("\n", " ");
                    if (p.isNullable)
                    {
                        comment += " 此参数可以为空";
                    }
                }
                comment += "\n*/";

                var codeitem = new CodeItem($"static async {method.title}(component: IHttpClientUsing{(!string.IsNullOrEmpty(parameterString) ? ",":"")}{parameterString}): Promise<{returnType}>");
                codeitem.Comment = comment;

                  invokeParameterStr = $"[{invokeParameterStr}]";

                string body = @"
        return new Promise(async (resolve, reject) => {
            try {
                var url = `${ApiHelper.UrlAddresses.currentUrls.v3Api}/JMSRedirect/" + _controllerInfo.name + @"/" + method.title + @"`;

                var ret = await HttpClient.postJsonAsync({
                    data: " + invokeParameterStr + @",
                    component: component,
                    url: url,
                    header: {
                        Authorization : `Bearer ${(ApiHelper.CurrentTokenInfo?ApiHelper.CurrentTokenInfo.access_token:'')}`
                    },
                });

                var obj: ServerResult;
                eval(""obj="" + ret);
                if (obj.code != 200) {
                    console.log(""调用接口" + method.title + @"出错，参数: "" + JSON.stringify(" + invokeParameterStr + @") + ""，服务器返回："" + ret);
                    throw obj;
                }
                else {
                    resolve(" + (method.returnData == null ? "" : "obj.data") + @");
}
            }
            catch (e) {
                console.log(""调用接口" + method.title + @"出错，参数: "" + JSON.stringify(" + invokeParameterStr + @") );
                reject(e);
            }
        });
";

                if (body != null)
                {
                    codeitem.AddString(body);
                }

                classCodeItem.AddItem(codeitem);
            }

            StringBuilder methodDescBuffer = new StringBuilder();
            methodDescBuffer.AppendLine("/** 本页所有代码是自动生成的，请勿修改 **/");
            StringBuilder result = new StringBuilder();
            foreach (var code in methodComments)
            {
                methodDescBuffer.AppendLine(code);
            }

            result.AppendLine(classCodeItem.Build());

            if(_controllerInfo.dataTypeInfos != null)
            {
                foreach( var typeinfo in _controllerInfo.dataTypeInfos)
                {
                    result.AppendLine(BuildType(typeinfo));
                }
            }

            return methodDescBuffer + @"
import { ApiHelper,ServerResult } from ""./ApiHelper"";
import { HttpClient } from ""jack-one-script"";
import { IHttpClientUsing } from ""jack-one-script"";
import { BaseComponent } from ""../BaseComponent"";
" + result.ToString();
        }
    }
}
