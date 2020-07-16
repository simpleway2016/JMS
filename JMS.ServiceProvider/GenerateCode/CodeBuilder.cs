using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json.Schema;
using System.Xml;

namespace JMS.GenerateCode
{
    class CodeBuilder : ICodeBuilder
    {
        MicroServiceHost _microServiceProvider;
        public CodeBuilder(MicroServiceHost microServiceProvider)
        {
            _microServiceProvider = microServiceProvider;
        }

        void AddComment(CodeMemberMethod codeMethod,MethodInfo methodInfo,XmlElement parentEle)
        {
            if (parentEle == null)
                return;

            XmlElement commentEle = null;
            foreach( XmlElement node in parentEle.ChildNodes )
            {
                if( node.Name == "member" && node.Attributes["name"].InnerText.StartsWith($"M:{methodInfo.DeclaringType.FullName}.{methodInfo.Name}"))
                {
                    commentEle = node;
                    break;
                }
            }

            if(commentEle != null)
            {
                try
                {
                    foreach( XmlElement ele in commentEle.ChildNodes)
                    {
                        codeMethod.Comments.Add(new CodeCommentStatement(ele.OuterXml, true));
                    }
                }
                catch 
                {
                }
               
            }

        }

        CodeMemberMethod getMethodCode(MethodInfo methodInfo,bool isAsync)
        {
            var parameters = methodInfo.GetParameters();
            if (parameters.Length > 0 && parameters[0].ParameterType == typeof(TransactionDelegate))
            {
                parameters = parameters.Skip(1).ToArray();
            }
            CodeExpression[] codeParamExps = new CodeExpression[parameters.Length + 1];
            codeParamExps[0] = new CodePrimitiveExpression(methodInfo.Name);
            for (int i = 0; i < parameters.Length; i++)
            {
                codeParamExps[i + 1] = new CodeFieldReferenceExpression(null, parameters[i].Name);
            }

            var codeMethod = CodeHelper.GetCodeMethod(methodInfo, parameters);
            if(isAsync)
            {
                codeMethod.Name = methodInfo.Name + "Async";
                Type returnType = null;
                if (methodInfo.ReturnType.FullName != "System.Void")
                    returnType = typeof(Task<>).MakeGenericType(methodInfo.ReturnType);
                else
                    returnType = typeof(Task);
                codeMethod.ReturnType = new CodeTypeReference(returnType);
            }


            var methodRef1 = new CodeMethodReferenceExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_microService"), isAsync? "InvokeAsync" : "Invoke");
            CodeMethodInvokeExpression invokeExp = new CodeMethodInvokeExpression(methodRef1, codeParamExps);

            if (methodInfo.ReturnType.FullName != "System.Void")
            {
                methodRef1.TypeArguments.Add(new CodeTypeReference(methodInfo.ReturnType));                
                CodeMethodReturnStatement returnExp = new CodeMethodReturnStatement(invokeExp);
                codeMethod.Statements.Add(returnExp);
            }
            else
            {
                if (isAsync)
                {
                    CodeMethodReturnStatement returnExp = new CodeMethodReturnStatement(invokeExp);
                    codeMethod.Statements.Add(returnExp);
                }
                else
                {
                    codeMethod.Statements.Add(invokeExp);
                }
            }
           

            return codeMethod;
        }

        public string GenerateCode(string nameSpace,string className, string serviceName)
        {

            var controllerTypeInfo = _microServiceProvider.ServiceNames[serviceName];
            System.Xml.XmlDocument xmldoc = null;
            var xmlpath = $"{Path.GetDirectoryName(controllerTypeInfo.Type.Assembly.Location)}/{Path.GetFileNameWithoutExtension(controllerTypeInfo.Type.Assembly.Location)}.xml";
            if(File.Exists(xmlpath))
            {
                xmldoc = new System.Xml.XmlDocument();
                xmldoc.Load(xmlpath);
            }
            XmlElement memberXmlNodeList = null;
            if(xmldoc != null)
                memberXmlNodeList = (XmlElement)xmldoc.DocumentElement.SelectSingleNode("members");

            var methods = controllerTypeInfo.Methods;

            //https://docs.microsoft.com/zh-cn/dotnet/api/system.codedom.codepropertysetvaluereferenceexpression?view=dotnet-plat-ext-3.1
            CodeCompileUnit unit = new CodeCompileUnit();

            //设置命名空间（这个是指要生成的类的空间）

            CodeNamespace codeNamespace = new CodeNamespace(nameSpace);
            unit.Namespaces.Add(codeNamespace);

            codeNamespace.Imports.Add(new CodeNamespaceImport("JMS"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Linq"));

            CodeTypeDeclaration myClass = new CodeTypeDeclaration(className);
            myClass.BaseTypes.Add(new CodeTypeReference("IImplInvoker"));
            codeNamespace.Types.Add(myClass);

            //添加ServiceName attribute
            CodeAttributeDeclaration attr = new CodeAttributeDeclaration("InvokerInfo" , new CodeAttributeArgument(new CodePrimitiveExpression(serviceName)));
            myClass.CustomAttributes.Add(attr);

            foreach (var methodInfo in methods)
            {
                var methodcode = getMethodCode(methodInfo, false);
                AddComment(methodcode, methodInfo,memberXmlNodeList);
                myClass.Members.Add(methodcode);

                methodcode = getMethodCode(methodInfo, true);
                AddComment(methodcode, methodInfo,memberXmlNodeList);
                myClass.Members.Add(methodcode);
            }

            CodeMemberField field = new CodeMemberField("JMS.IMicroService", "_microService");
            field.Attributes = MemberAttributes.Family;
            myClass.Members.Add(field);

            ///将构造方法添加到类中
            CodeConstructor constructor = new CodeConstructor();
            constructor.Attributes = MemberAttributes.Public;
            constructor.Parameters.Add(new CodeParameterDeclarationExpression("JMS.IMicroService" , "microService"));
            constructor.Statements.Add(new CodeAssignStatement(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression() , "_microService") ,
                new CodeFieldReferenceExpression(null, "microService")
                ));
            myClass.Members.Add(constructor);


            //添加特特性
            //myClass.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(SerializeField))));


            CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");


            CodeGeneratorOptions options = new CodeGeneratorOptions();

            //代码风格:大括号的样式{}
            options.BracingStyle = "C";
            //是否在字段、属性、方法之间添加空白行
            options.BlankLinesBetweenMembers = true;

          
            //保存
            using (var ms = new System.IO.MemoryStream())
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(ms,Encoding.UTF8))
            {
               provider.GenerateCodeFromCompileUnit(unit, sw, options);
                sw.Flush();
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

    }
}
