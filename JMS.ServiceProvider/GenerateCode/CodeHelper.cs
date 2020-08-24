using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

namespace JMS.GenerateCode
{
    class CodeHelper
    {
        public static ThreadLocal<Type> CurrentControllerType = new ThreadLocal<Type>();
        public static ThreadLocal<Dictionary<Type, string>> CurrentCreatedSubTypes = new ThreadLocal<Dictionary<Type, string>>();
        public static ThreadLocal<CodeTypeDeclaration> CurrentClassCode = new ThreadLocal<CodeTypeDeclaration>();
        public static ThreadLocal<XmlElement> CurrentXmlMembersElement = new ThreadLocal<XmlElement>();
        public static CodeMemberMethod GetCodeMethod(MethodInfo method, ParameterInfo[] parameters)
        {
            CodeMemberMethod codeMethod = new CodeMemberMethod();
            codeMethod.Attributes = MemberAttributes.Public;
            codeMethod.Name = method.Name;
            codeMethod.ReturnType = GetTypeCode(method.ReturnType , true);
            foreach (var p in parameters)
            {
                codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(GetTypeCode(p.ParameterType), p.Name));

            }

            return codeMethod;
        }

        public static CodeTypeReference GetTypeCode(Type type,bool findSubClass = false)
        {
            if (type.IsArray)
            {
                CodeTypeReference genRet = new CodeTypeReference();
                genRet.ArrayRank = type.GetArrayRank();
                genRet.ArrayElementType = GetTypeCode(type.GetElementType() , findSubClass);
                return genRet;
            }
            else if (type.IsGenericType)
            {
                Type[] argTypes = type.GetGenericArguments();
                CodeTypeReference[] codeArgTypes = argTypes.Select(m => GetTypeCode(m , findSubClass)).ToArray();

                CodeTypeReference genRet = new CodeTypeReference();
                genRet.TypeArguments.AddRange(codeArgTypes);
                genRet.BaseType = type.FullName;
                return genRet;
            }

            if (type.IsEnum
                   && CurrentControllerType.Value.Assembly == type.Assembly)
            {
                //生成这个类代码
                string strProType = BuildEnumTypeCode(type);
                return new CodeTypeReference(strProType);
            }
            else if (type.IsValueType == false && type != typeof(object) && type != typeof(string)
                    && CurrentControllerType.Value.Assembly == type.Assembly)
            {
                //生成这个类代码
                string strProType = BuildTypeCode(type , findSubClass);
                return new CodeTypeReference(strProType);
            }
            else
            {
                return new CodeTypeReference(type);
            }
        }
        public static string BuildEnumTypeCode(Type type)
        {
            if (CurrentCreatedSubTypes.Value.ContainsKey(type))
                return CurrentCreatedSubTypes.Value[type];

            CurrentCreatedSubTypes.Value[type] = type.Name;

            CodeTypeDeclaration myClass = new CodeTypeDeclaration(type.Name);
            myClass.IsEnum = true;

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                CodeMemberField codePro = new CodeMemberField();
                myClass.Members.Add(codePro);
                codePro.Name = field.Name;

                foreach (XmlElement node in CurrentXmlMembersElement.Value.ChildNodes)
                {
                    if (node.Name == "member" && node.Attributes["name"].InnerText.StartsWith($"F:{field.DeclaringType.FullName}.{field.Name}"))
                    {
                        try
                        {
                            codePro.Comments.Add(new CodeCommentStatement(node.SelectSingleNode("summary").OuterXml, true));
                        }
                        catch (Exception ex)
                        {
                        }
                        break;
                    }
                }

                codePro.InitExpression = new CodePrimitiveExpression((int)field.GetValue(null));
            }

            CurrentClassCode.Value.Members.Insert(0, myClass);

            return type.Name;
        }
        public static string BuildTypeCode(Type type, bool findSubClass = false)
        {
            if (CurrentCreatedSubTypes.Value.ContainsKey(type))
            {
                if (findSubClass)
                {
                    //删除现有的
                    CurrentCreatedSubTypes.Value.Remove(type);
                    foreach( var member in CurrentClassCode.Value.Members  )
                    {
                        if(member is CodeTypeDeclaration typedesc && typedesc.Name == type.Name)
                        {
                            CurrentClassCode.Value.Members.Remove(typedesc);
                            break;
                        }
                    }
                }
                else
                {
                    return CurrentCreatedSubTypes.Value[type];
                }
            }

            CurrentCreatedSubTypes.Value[type] = type.Name;


            CodeTypeDeclaration myClass = new CodeTypeDeclaration(type.Name);
            myClass.Attributes = MemberAttributes.Public;
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if(findSubClass)
            {
                List<PropertyInfo> list = new List<PropertyInfo>(properties);
                //需要查找继承类
               var subTypes = type.Assembly.DefinedTypes.Where(m => m.IsSubclassOf(type)).ToArray();
                foreach( var subtype in subTypes )
                {
                    var subPros = subtype.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where( m=>properties.Any(n=>n.Name == m.Name) == false ).ToArray();
                    list.AddRange(subPros);
                }
                properties = list.ToArray();
            }

            var parentType = type.BaseType;
            while(parentType != null && parentType != typeof(object))
            {
                if (parentType.FullName == "Way.EntityDB.DataItem")
                {
                    properties = properties.Where(m => m.DeclaringType.FullName != "Way.EntityDB.DataItem").ToArray();
                    break;
                }
                else
                    parentType = parentType.BaseType;
            }

            foreach( var pro in properties )
            {
                CodeMemberProperty codePro = new CodeMemberProperty();
                CodeMemberField codeField = new CodeMemberField();
                myClass.Members.Add(codeField);
                myClass.Members.Add(codePro);
                codePro.Name = pro.Name;
                codeField.Name = "_" + codePro.Name;
                codePro.Attributes = MemberAttributes.Public;
                codePro.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(null, "_" + codePro.Name)));
                codePro.SetStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(null, "_" + codePro.Name), new CodeFieldReferenceExpression(null, "value")));

                foreach (XmlElement node in CurrentXmlMembersElement.Value.ChildNodes)
                {
                    if (node.Name == "member" && node.Attributes["name"].InnerText.StartsWith($"P:{pro.DeclaringType.FullName}.{pro.Name}"))
                    {
                        try
                        {
                            codePro.Comments.Add(new CodeCommentStatement(node.SelectSingleNode("summary").OuterXml, true));
                        }
                        catch (Exception ex)
                        {
                        }                        
                        break;
                    }
                }

                var proType = pro.PropertyType;
                codePro.Type = GetTypeCode(proType);
                codeField.Type = codePro.Type;
            }


            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                CodeMemberProperty codePro = new CodeMemberProperty();
                CodeMemberField codeField = new CodeMemberField();
                myClass.Members.Add(codeField);
                myClass.Members.Add(codePro);
                codePro.Name = field.Name;
                codeField.Name = "_" + codePro.Name;
                codePro.Attributes = MemberAttributes.Public;
                codePro.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(null, "_" + codePro.Name)));
                codePro.SetStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(null, "_" + codePro.Name), new CodeFieldReferenceExpression(null, "value")));

                foreach (XmlElement node in CurrentXmlMembersElement.Value.ChildNodes)
                {
                    if (node.Name == "member" && node.Attributes["name"].InnerText.StartsWith($"F:{field.DeclaringType.FullName}.{field.Name}"))
                    {
                        try
                        {
                            codePro.Comments.Add(new CodeCommentStatement(node.SelectSingleNode("summary").OuterXml, true));
                        }
                        catch (Exception ex)
                        {
                        }
                        break;
                    }
                }

                var proType = field.FieldType;
                codePro.Type = GetTypeCode(proType);
                codeField.Type = codePro.Type;
            }

            CurrentClassCode.Value.Members.Insert(0 , myClass);

            return type.Name;
        }
    }
}
