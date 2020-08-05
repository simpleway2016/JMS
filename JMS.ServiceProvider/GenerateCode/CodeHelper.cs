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
        public static ThreadLocal<Dictionary<Type,string>> CurrentCreatedSubTypes = new ThreadLocal<Dictionary<Type, string>>();
        public static ThreadLocal<CodeTypeDeclaration> CurrentClassCode = new ThreadLocal<CodeTypeDeclaration>();
        public static ThreadLocal<XmlElement> CurrentXmlMembersElement = new ThreadLocal<XmlElement>();
        public static CodeMemberMethod GetCodeMethod( MethodInfo method,ParameterInfo[] parameters)
        {
            CodeMemberMethod codeMethod = new CodeMemberMethod();
            codeMethod.Attributes = MemberAttributes.Public;
            codeMethod.Name = method.Name;
            codeMethod.ReturnType = GetTypeCode(method.ReturnType);
            foreach( var p in parameters )
            {
                codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(GetTypeCode(p.ParameterType), p.Name));

            }

            return codeMethod;
        }

        public static CodeTypeReference GetTypeCode(Type type)
        {
            if(type.IsArray)
            {
                CodeTypeReference genRet = new CodeTypeReference();
                genRet.ArrayRank = type.GetArrayRank();
                genRet.ArrayElementType = GetTypeCode( type.GetElementType() );
                return genRet;
            }
            else if(type.IsGenericType)
            {
               Type[] argTypes = type.GetGenericArguments();
                CodeTypeReference[] codeArgTypes = argTypes.Select(m => GetTypeCode(m)).ToArray();

                CodeTypeReference genRet = new CodeTypeReference();
                genRet.TypeArguments.AddRange(codeArgTypes);
                genRet.BaseType = type.FullName;
                return genRet;
            }

            if (type.IsValueType == false && type != typeof(object) && type != typeof(string)
                    && CurrentControllerType.Value.Assembly == type.Assembly)
            {
                //生成这个类代码
                string strProType = BuildTypeCode(type);
                return new CodeTypeReference(strProType);
            }
            else
            {
                return new CodeTypeReference(type);
            }
        }

        public static string BuildTypeCode(Type type)
        {
            if (CurrentCreatedSubTypes.Value.ContainsKey(type))
                return CurrentCreatedSubTypes.Value[type];

            CurrentCreatedSubTypes.Value[type] = type.Name;

            CodeTypeDeclaration myClass = new CodeTypeDeclaration(type.Name);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach( var pro in properties )
            {
                CodeMemberProperty codePro = new CodeMemberProperty();
                myClass.Members.Add(codePro);
                codePro.Name = pro.Name;

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
                if (proType.IsValueType == false && proType != typeof(object) && proType != typeof(string)
                    && CurrentControllerType.Value.Assembly == proType.Assembly)
                {
                    //生成这个类代码
                    string strProType = BuildTypeCode(proType);
                    codePro.Type = new CodeTypeReference(strProType);

                }
                else
                {
                    codePro.Type = new CodeTypeReference(proType);
                }
            }


            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                CodeMemberProperty codePro = new CodeMemberProperty();
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

                var proType = field.FieldType;
                if (proType.IsValueType == false && proType != typeof(object) && proType != typeof(string)
                    && CurrentControllerType.Value.Assembly == proType.Assembly)
                {
                    //生成这个类代码
                    string strProType = BuildTypeCode(proType);
                    codePro.Type = new CodeTypeReference(strProType);

                }
                else
                {
                    codePro.Type = new CodeTypeReference(proType);
                }
            }

            CurrentClassCode.Value.Members.Insert(0 , myClass);

            return type.Name;
        }
    }
}
