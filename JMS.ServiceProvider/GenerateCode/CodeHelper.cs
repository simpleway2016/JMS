using JMS.AssemblyDocumentReader;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

namespace JMS.GenerateCode
{
    class CodeHelper
    {
        public static AsyncLocal<Type> CurrentControllerType = new AsyncLocal<Type>();
        public static AsyncLocal<Dictionary<Type, string>> CurrentCreatedSubTypes = new AsyncLocal<Dictionary<Type, string>>();
        public static AsyncLocal<CodeTypeDeclaration> CurrentClassCode = new AsyncLocal<CodeTypeDeclaration>();

        public static CodeMemberMethod GetCodeMethod(MethodInfo method, ParameterInfo[] parameters)
        {
            CodeMemberMethod codeMethod = new CodeMemberMethod();
            codeMethod.Attributes = MemberAttributes.Public;
            codeMethod.Name = method.Name;

            bool isNullableReturn = false;
            var attArr = method.GetCustomAttributes().ToArray();
            if (attArr.Any(m => m.GetType().Name == "NullableContextAttribute"))
            {
                isNullableReturn = true;
            }

            codeMethod.ReturnType = GetTypeCode(TypeInfoBuilder.GetReturnType(method.ReturnType), true , isNullableReturn);
            foreach (var p in parameters)
            {
                bool isNullableParam = false;
                attArr = p.GetCustomAttributes().ToArray();
                if (attArr.Any(m => m.GetType().Name == "NullableAttribute"))
                {
                    isNullableParam = true;
                }

                codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(GetTypeCode(p.ParameterType,false, isNullableParam), p.Name));

            }

            return codeMethod;
        }


        public static CodeTypeReference GetTypeCode(Type type, bool findSubClass = false , bool isNullable = false)
        {
            if (type.IsArray)
            {
                CodeTypeReference genRet = new CodeTypeReference();
                genRet.ArrayRank = type.GetArrayRank();
                genRet.ArrayElementType = GetTypeCode(type.GetElementType(), findSubClass);
                return genRet;
            }
            else if (type != typeof(string) && type.GetInterface(typeof(IEnumerable).FullName) != null)
            {
                if (type.IsGenericType && type.GetInterfaces().Contains(typeof(IDictionary)))
                {
                    var eleTypes = type.GetGenericArguments();
                    var newType = typeof(Dictionary<,>).MakeGenericType(eleTypes);
                    var ret = new CodeTypeReference(newType);
                    for (int i = 0; i < ret.TypeArguments.Count; i++)
                    {
                        ret.TypeArguments[i] = GetTypeCode(eleTypes[i],findSubClass);
                    }
                }
                else if (type.IsGenericType)
                {
                    Type[] argTypes = type.GetGenericArguments();
                    CodeTypeReference genRet = new CodeTypeReference();
                    genRet.ArrayRank = 1;
                    genRet.ArrayElementType = GetTypeCode(argTypes[0], findSubClass);
                    return genRet;
                }
                else
                {
                    CodeTypeReference genRet = new CodeTypeReference();
                    genRet.ArrayRank = 1;
                    genRet.ArrayElementType = new CodeTypeReference(typeof(object));
                    return genRet;
                }
            }
            else if (type.IsGenericType)
            {
                Type[] argTypes = type.GetGenericArguments();
                CodeTypeReference[] codeArgTypes = argTypes.Select(m => GetTypeCode(m, findSubClass)).ToArray();

                if (type.Assembly == CurrentControllerType.Value.Assembly)
                {
                    string strProType = BuildTypeCode(type, findSubClass);
                    return new CodeTypeReference(strProType);
                }
                else
                {
                    CodeTypeReference genRet = new CodeTypeReference();
                    genRet.TypeArguments.AddRange(codeArgTypes);
                    genRet.BaseType = type.FullName;
                    return genRet;
                }
            }

            if (type.IsEnum
                   && CurrentControllerType.Value.Assembly == type.Assembly)
            {
                //生成这个类代码
                string strProType = BuildEnumTypeCode(type);
                return new CodeTypeReference(strProType);
            }
            else if (type.IsPrimitive == false && type != typeof(object) && type != typeof(string)
                    && (CurrentControllerType.Value == null || CurrentControllerType.Value.Assembly == type.Assembly))
            {
                //生成这个类代码
                string strProType = BuildTypeCode(type, findSubClass);
                return new CodeTypeReference(strProType);
            }
            else
            {
                if(isNullable && type == typeof(string)  )
                {
                    return new CodeTypeReference("string?");
                }
                return new CodeTypeReference(type);
            }
        }
        public static string BuildEnumTypeCode(Type type)
        {
            if (CurrentCreatedSubTypes.Value.ContainsKey(type))
                return CurrentCreatedSubTypes.Value[type];

            CurrentCreatedSubTypes.Value[type] = type.Name;

            var typeDoc = AssemblyDocumentReader.DocumentReader.GetTypeDocument(type);
            CodeTypeDeclaration myClass = new CodeTypeDeclaration(type.Name);
            myClass.IsEnum = true;
            if (typeDoc != null)
            {
                myClass.Comments.Add(new CodeCommentStatement(typeDoc.GetXmlComment(), true));
            }
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);



            foreach (var field in fields)
            {
                if (field.IsSpecialName)
                    continue;

                CodeMemberField codePro = new CodeMemberField();
                myClass.Members.Add(codePro);
                codePro.Name = field.Name;

                if (typeDoc != null)
                {
                    var fieldDoc = typeDoc.Fields.FirstOrDefault(m => m.Name == field.Name);

                    if (fieldDoc != null)
                    {
                        try
                        {
                            codePro.Comments.Add(new CodeCommentStatement(fieldDoc.GetXmlComment(), true));
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }

                codePro.InitExpression = new CodePrimitiveExpression((int)field.GetValue(null));
            }

            CurrentClassCode.Value.Members.Insert(0, myClass);

            return type.Name;
        }
        public static string BuildTypeCode(Type type, bool findSubClass = false)
        {
            try
            {
                var typeDoc = DocumentReader.GetTypeDocument(type);
                var typename = type.Name;
                if (type.IsGenericType)
                {
                    var genericTypes = type.GetGenericArguments();

                    typename = typename.Substring(0, type.Name.Length - 2);
                    foreach (var gtype in genericTypes)
                    {
                        if (CurrentCreatedSubTypes.Value.ContainsKey(gtype))
                        {
                            typename += "_" + CurrentCreatedSubTypes.Value[gtype];
                        }
                        else
                        {
                            typename += "_" + gtype.Name;
                        }
                    }
                }

                if (CurrentCreatedSubTypes.Value != null & CurrentCreatedSubTypes.Value.ContainsKey(type))
                {
                    if (findSubClass)
                    {
                        //删除现有的
                        CurrentCreatedSubTypes.Value.Remove(type);
                        foreach (var member in CurrentClassCode.Value.Members)
                        {
                            if (member is CodeTypeDeclaration typedesc && typedesc.Name == typename)
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


                CurrentCreatedSubTypes.Value[type] = typename;


                CodeTypeDeclaration myClass = new CodeTypeDeclaration(typename);
                myClass.Attributes = MemberAttributes.Public;
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (findSubClass)
                {
                    List<PropertyInfo> list = new List<PropertyInfo>(properties);
                    //需要查找继承类
                    var subTypes = type.Assembly.DefinedTypes.Where(m => m.IsPublic && m.IsSubclassOf(type)).ToArray();
                    foreach (var subtype in subTypes)
                    {
                        var subPros = subtype.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(m => list.Any(n => n.Name == m.Name) == false).ToArray();
                        list.AddRange(subPros);
                    }
                    properties = list.ToArray();
                }

                var parentType = type.BaseType;
                while (parentType != null && parentType != typeof(object))
                {
                    if (parentType.FullName == "Way.EntityDB.DataItem")
                    {
                        properties = properties.Where(m => m.DeclaringType.FullName != "Way.EntityDB.DataItem").ToArray();
                        break;
                    }
                    else
                        parentType = parentType.BaseType;
                }


                foreach (var pro in properties)
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

                    if (typeDoc != null)
                    {
                        var prodoc = typeDoc.Properties.FirstOrDefault(m => m.Name == pro.Name);
                        if (prodoc != null)
                        {
                            codePro.Comments.Add(new CodeCommentStatement(prodoc.GetXmlComment(), true));
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

                    if (typeDoc != null)
                    {
                        var prodoc = typeDoc.Fields.FirstOrDefault(m => m.Name == field.Name);
                        if (prodoc != null)
                        {
                            codePro.Comments.Add(new CodeCommentStatement(prodoc.GetXmlComment(), true));
                        }
                    }

                    var proType = field.FieldType;
                    codePro.Type = GetTypeCode(proType);
                    codeField.Type = codePro.Type;
                }

                CurrentClassCode.Value.Members.Insert(0, myClass);

                return typename;
            }
            catch (Exception)
            {

                throw;
            }

        }
    }
}
