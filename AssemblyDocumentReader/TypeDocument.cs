using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace JMS.AssemblyDocumentReader
{
    public class TypeDocument
    {
        public Type Type { get; }
        public string Comment { get; set; }
        public List<MethodDocument> Methods { get; }
        public List<ItemDocument> Properties { get; }
        public List<ItemDocument> Fields { get; }
        public TypeDocument(Type type)
        {
            this.Type = type;
            this.Properties = new List<ItemDocument>();
            this.Fields = new List<ItemDocument>();
            this.Methods = new List<MethodDocument>();
        }

        public override string ToString()
        {
            return Type.Name;
        }

        /// <summary>
        /// 返回xml格式的注释
        /// </summary>
        /// <returns></returns>
        public string GetXmlComment()
        {
            XmlDocument xmldoc = new XmlDocument();
            var root = xmldoc.CreateElement("root");

            var summaryEle = xmldoc.CreateElement("summary");
            summaryEle.InnerText = this.Comment;
            root.AppendChild(summaryEle);


            return root.ToXmlString();
        }
    }

    public class ItemDocument
    {
        public string Name { get; }
        public string Comment { get; set; }
        public ItemDocument(string name)
        {
            this.Name = name;
        }
        /// <summary>
        /// 返回xml格式的注释
        /// </summary>
        /// <returns></returns>
        public string GetXmlComment()
        {
            XmlDocument xmldoc = new XmlDocument();
            var root = xmldoc.CreateElement("root");

            var summaryEle = xmldoc.CreateElement("summary");
            summaryEle.InnerText = this.Comment;
            root.AppendChild(summaryEle);


            return root.ToXmlString();
        }
        public override string ToString()
        {
            return Name;
        }
    }
}
