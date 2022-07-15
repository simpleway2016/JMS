using JMS.Domains;
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
using JMS.AssemblyDocumentReader;
using JMS.Infrastructures;

namespace JMS.GenerateCode
{
    class CodeBuilder : ICodeBuilder
    {
        MicroServiceHost _microServiceProvider;
        public CodeBuilder(MicroServiceHost microServiceProvider)
        {
            _microServiceProvider = microServiceProvider;
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
            var originalRetType = codeMethod.ReturnType;
            if(isAsync)
            {
                codeMethod.Name = methodInfo.Name + "Async";
                if (methodInfo.ReturnType.FullName != "System.Void")
                {
                    codeMethod.ReturnType = new CodeTypeReference();
                    codeMethod.ReturnType.TypeArguments.Add(originalRetType);
                    codeMethod.ReturnType.BaseType = "Task";
                }
                else
                    codeMethod.ReturnType = new CodeTypeReference( typeof(Task));
            }


            var methodRef1 = new CodeMethodReferenceExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_microService"), isAsync? "InvokeAsync" : "Invoke");
            CodeMethodInvokeExpression invokeExp = new CodeMethodInvokeExpression(methodRef1, codeParamExps);

            if (methodInfo.ReturnType.FullName != "System.Void")
            {
                methodRef1.TypeArguments.Add(originalRetType);                
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
            CodeHelper.CurrentCreatedSubTypes.Value = new Dictionary<Type, string>();
            var controllerTypeInfo = _microServiceProvider.ServiceNames[serviceName];
            CodeHelper.CurrentControllerType.Value = controllerTypeInfo.Type;

            var typeDoc = DocumentReader.GetTypeDocument(controllerTypeInfo.Type);

            var methods = controllerTypeInfo.Methods.Select(m=>m.Method).ToArray();

            //https://docs.microsoft.com/zh-cn/dotnet/api/system.codedom.codepropertysetvaluereferenceexpression?view=dotnet-plat-ext-3.1
            CodeCompileUnit unit = new CodeCompileUnit();

            //设置命名空间（这个是指要生成的类的空间）

            CodeNamespace codeNamespace = new CodeNamespace(nameSpace);
            unit.Namespaces.Add(codeNamespace);

            codeNamespace.Imports.Add(new CodeNamespaceImport("JMS"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Linq"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Threading.Tasks"));

            CodeTypeDeclaration myClass = new CodeTypeDeclaration(className);
            CodeHelper.CurrentClassCode.Value = myClass;
            myClass.BaseTypes.Add(new CodeTypeReference("IImplInvoker"));
            codeNamespace.Types.Add(myClass);

            //添加ServiceName attribute
            CodeAttributeDeclaration attr = new CodeAttributeDeclaration("InvokerInfo" , new CodeAttributeArgument(new CodePrimitiveExpression(serviceName)));
            myClass.CustomAttributes.Add(attr);

            foreach (var methodInfo in methods)
            {
                var methodDoc = typeDoc.Methods.FirstOrDefault(m => m.MethodInfo == methodInfo);
               
                var methodcode = getMethodCode(methodInfo, false);
                if (methodDoc != null)
                {
                    methodcode.Comments.Add(new CodeCommentStatement(methodDoc.GetXmlComment(), true));
                }
                myClass.Members.Add(methodcode);

                codeNamespace.Comments.Add(new CodeCommentStatement(methodDoc.Name + "   " + methodDoc.Comment));

                methodcode = getMethodCode(methodInfo, true);
                if (methodDoc != null)
                {
                    methodcode.Comments.Add(new CodeCommentStatement(methodDoc.GetXmlComment(), true));
                }
                myClass.Members.Add(methodcode);
            }

            CodeMemberField field = new CodeMemberField("JMS.IMicroService", "_microService");
            field.Attributes = MemberAttributes.Family;
            myClass.Members.Add(field);

            CodeMemberProperty pro = new CodeMemberProperty();
            pro.Attributes = MemberAttributes.Public;
            pro.Type = new CodeTypeReferenceExpression("JMS.IMicroService").Type;
            pro.Name = "RemoteClient";
            pro.HasSet = false;
            pro.HasGet = true;
            pro.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(null, "_microService")));
            myClass.Members.Add(pro);

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
