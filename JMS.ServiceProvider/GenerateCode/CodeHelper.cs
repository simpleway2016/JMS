using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace JMS.GenerateCode
{
    class CodeHelper
    {
        public static CodeMemberMethod GetCodeMethod(MethodInfo method,ParameterInfo[] parameters)
        {
            CodeMemberMethod codeMethod = new CodeMemberMethod();
            codeMethod.Attributes = MemberAttributes.Public;
            codeMethod.Name = method.Name;
            codeMethod.ReturnType = new CodeTypeReference(method.ReturnType);
            foreach( var p in parameters )
            {
                codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(p.ParameterType, p.Name));
            }

            return codeMethod;
        }
    }
}
