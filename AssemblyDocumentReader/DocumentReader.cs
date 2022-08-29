using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

namespace JMS.AssemblyDocumentReader
{
    public class DocumentReader
    {
        static ConcurrentDictionary<string, TypeDocument> AllTypeDocs = new ConcurrentDictionary<string, TypeDocument>();
        public static TypeDocument GetTypeDocument(Type type)
        {
            var fullname = type.FullName;
            if (fullname.Contains("["))
                fullname = fullname.Substring(0, fullname.IndexOf("["));

            if (AllTypeDocs.ContainsKey(fullname))
                return AllTypeDocs[fullname];

            TypeDocument typeDocument = new TypeDocument(type);
            AllTypeDocs[fullname] = typeDocument;

            var xmldoc = XmlNodeFactory.GetTypeXmlNode(type);
            typeDocument.Comment = GetTypeCommentString(xmldoc, type);

            var pros = type.GetProperties();
            foreach (var pro in pros)
            {
                var proDoc = new ItemDocument(pro.Name);
                typeDocument.Properties.Add(proDoc);

                proDoc.Comment = GetProCommentString(pro.DeclaringType, pro.Name);
            }

            var fields = type.GetFields();
            foreach (var field in fields)
            {
                if (field.IsSpecialName)
                    continue;
                var fDoc = new ItemDocument(field.Name);
                typeDocument.Fields.Add(fDoc);

                fDoc.Comment = GetFieldCommentString(field.DeclaringType, field.Name);
            }

            if (type.IsEnum == false)
            {
                var methods = type.GetMethods();
                foreach (var method in methods)
                {
                    if (method.IsSpecialName)
                        continue;

                    var methodDoc = new MethodDocument(method);
                    typeDocument.Methods.Add(methodDoc);

                    methodDoc.Comment = GetMethodCommentString(method.DeclaringType, method, out XmlNode methodXmlNode);

                    var parameters = method.GetParameters();

                    if(methodXmlNode != null)
                    {
                        var typeparamNodes = methodXmlNode.SelectNodes("typeparam");
                        foreach( XmlElement tnode in typeparamNodes)
                        {
                            var tpDoc = new ItemDocument(tnode.GetAttribute("name"));
                            methodDoc.TypeParameters.Add(tpDoc);
                            tpDoc.Comment = tnode.InnerText?.Trim();
                        }
                    }

                    foreach (var p in parameters)
                    {
                        var pdoc = new ItemDocument(p.Name);
                        methodDoc.Parameters.Add(pdoc);

                        if (methodXmlNode != null)
                        {
                            var pxmlnode = methodXmlNode.SelectSingleNode("param[@name='" + p.Name + "']");
                            if (pxmlnode != null)
                            {
                                pdoc.Comment = pxmlnode.InnerText?.Trim();
                            }
                        }
                    }

                    if (methodXmlNode != null)
                    {
                        var pxmlnode = methodXmlNode.SelectSingleNode("returns");
                        if (pxmlnode != null)
                        {
                            methodDoc.ReturnComment = pxmlnode.InnerText?.Trim();
                        }
                    }
                }
            }
            return typeDocument;
        }

        internal static string GetTypeCommentString(XmlNode xmldoc, Type type)
        {
            if (xmldoc == null)
                return null;
            var fullname = type.FullName;
            if (fullname.Contains("["))
                fullname = fullname.Substring(0, fullname.IndexOf("["));
            if (fullname.Contains("+"))
                fullname = fullname.Replace("+", ".");

            foreach (XmlNode node in xmldoc.ChildNodes)
            {
                if (node.Name == "member" && node.Attributes["name"].InnerText.Equals($"T:{fullname}"))
                {
                    try
                    {
                        return node.SelectSingleNode("summary").InnerText?.Trim();
                    }
                    catch (Exception ex)
                    {
                    }
                    break;
                }
            }
            return null;
        }

        internal static string GetProCommentString(Type type, string proName)
        {
            XmlNode xmldoc = XmlNodeFactory.GetTypeXmlNode(type);
            if (xmldoc == null)
                return null;

            var fullname = type.FullName;
            if (fullname.Contains("["))
                fullname = fullname.Substring(0, fullname.IndexOf("["));
            if (fullname.Contains("+"))
                fullname = fullname.Replace("+", ".");

            foreach (XmlNode node in xmldoc.ChildNodes)
            {
                if (node.Name == "member" && node.Attributes["name"].InnerText.Equals($"P:{fullname}.{proName}"))
                {
                    try
                    {
                        return node.SelectSingleNode("summary").InnerText?.Trim();
                    }
                    catch (Exception ex)
                    {
                    }
                    break;
                }
            }
            return null;
        }

        internal static string GetFieldCommentString(Type type, string fName)
        {
            XmlNode xmldoc = XmlNodeFactory.GetTypeXmlNode(type);
            if (xmldoc == null)
                return null;

            var fullname = type.FullName;
            if (fullname.Contains("["))
                fullname = fullname.Substring(0, fullname.IndexOf("["));
            if (fullname.Contains("+"))
                fullname = fullname.Replace("+", ".");

            foreach (XmlNode node in xmldoc.ChildNodes)
            {
                if (node.Name == "member" && node.Attributes["name"].InnerText.Equals($"F:{fullname}.{fName}"))
                {
                    try
                    {
                        return node.SelectSingleNode("summary").InnerText?.Trim();
                    }
                    catch (Exception ex)
                    {
                    }
                    break;
                }
            }
            return null;
        }

        internal static string GetParameterTypeString(Type ptype)
        {
            if(ptype.FullName == null)
            {
                return ptype.ToString();
            }
            if (ptype.IsGenericType == false)
                return ptype.FullName.Replace("+",".");
            else
            {
                StringBuilder buffer = new StringBuilder();
                var fullname = ptype.FullName;
                fullname = fullname.Substring(0, fullname.IndexOf("`"));
                fullname = fullname.Replace("+", ".");
                var middleStr = ptype.GenericTypeArguments.Select(m => GetParameterTypeString(m)).ToArray();
                return fullname + "{" + String.Join(",", middleStr) + "}";
            }
        }

        internal static string GetMethodCommentString(Type type, MethodInfo method, out XmlNode xmlNode)
        {
            xmlNode = null;
            XmlNode xmldoc = XmlNodeFactory.GetTypeXmlNode(type);
            if (xmldoc == null)
                return null;

            var parameters = method.GetParameters();

            string middle = "";
            var strArr = parameters.Select(m => GetParameterTypeString(m.ParameterType)).ToArray();
            if (parameters.Length > 0)
            {
                middle = "(" + String.Join(",", strArr) + ")";
            }
            foreach (XmlNode node in xmldoc.ChildNodes)
            {
                var fullname = type.FullName;
                if (fullname.Contains("["))
                    fullname = fullname.Substring(0, fullname.IndexOf("["));
                if (fullname.Contains("+"))
                    fullname = fullname.Replace("+", ".");


                string extenStr = "";
                if(method.IsGenericMethod)
                {
                    extenStr = $"``{method.GetGenericArguments().Length}";
                }
                if (node.Name == "member" && node.Attributes["name"].InnerText.Equals($"M:{fullname}.{method.Name}{extenStr}{middle}"))
                {
                    try
                    {
                        xmlNode = node;
                        return node.SelectSingleNode("summary").InnerText?.Trim();
                    }
                    catch (Exception ex)
                    {
                    }
                    break;
                }
            }

            return null;
        }
    }
}
