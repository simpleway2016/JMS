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
using System.Xml;
using System.Xml.Linq;
using Way.Lib;

namespace WebApiTest.GenerateCode
{
    public class GenerateRedirectCodeJavaScript
    {
        ControllerInfo _controllerInfo;

        public GenerateRedirectCodeJavaScript(string serviceInfo)
        {
            _controllerInfo = serviceInfo.FromJson<ControllerInfo>();
        }
        

        public string Build()
        {
            List<CodeItem> codeitems = new List<CodeItem>();
            List<string> methodComments = new List<string>();

            foreach (var method in _controllerInfo.items)
            {

                string comment = "/**";

                string parameterString = "";
                string invokeParameterStr = "";


                string methodComment = method.desc?.Replace("\r", "").Replace("\n", " ");
                comment += "\n* " + methodComment;
                var parameters = method.data.items;

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
                   
                    parameterString += pname ;
                    invokeParameterStr += p.name;

                   
                       
                        comment += "\n* @param " + p.name + " " + p.desc?.Replace("\r", "").Replace("\n", " ") ;

                    if(p.isNullable)
                    {
                        comment += " 此参数可以为空";
                    }

                }
                comment += "\n*/";

                var codeitem = new CodeItem($"export const {method.title} = ({parameterString}) =>");
                codeitem.Comment = comment;

              

                string body = null;

                if (true)
                {
                    body = @"
  return axios.post(`${tradebbUrl}/api/JMSRedirect/"+ _controllerInfo.name + @"/"+ method.title +@"`, ["+ invokeParameterStr + @"]);
";
                }

                if (body != null)
                {
                    codeitem.AddString(body);
                }

                codeitems.Add(codeitem);
            }

            StringBuilder methodDescBuffer = new StringBuilder();
            StringBuilder result = new StringBuilder();
            foreach (var code in methodComments)
            {
                methodDescBuffer.AppendLine(code);
            }

            foreach( var codeitem in codeitems )
            {
                result.AppendLine(codeitem.Build());
            }

            return methodDescBuffer + @"
import axios from ""./baseapi"";
import { tradebbUrl } from ""./baseUrl"";
" + result.ToString();
        }
    }
}
