using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace JMS.AssemblyDocumentReader
{
    public class MethodDocument
    {
        public MethodInfo MethodInfo  { get;}
        public string Name { get; set; }
        public string Comment { get; set; }
        public List<ItemDocument> Parameters { get; }
        /// <summary>
        /// 泛型方法里，泛型的注释
        /// </summary>
        public List<ItemDocument> TypeParameters { get; }
        public string ReturnComment { get; set; }
        public MethodDocument(MethodInfo method)
        {
            this.MethodInfo = method;
            this.Name = method.Name;
            this.Parameters = new List<ItemDocument>();
            this.TypeParameters = new List<ItemDocument>();
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

            foreach( var p in Parameters)
            {
                var paramEle = xmldoc.CreateElement("param");
                paramEle.InnerText = p.Comment;
                paramEle.SetAttribute("name", p.Name);
                root.AppendChild(paramEle);
            }

            if (!string.IsNullOrEmpty(ReturnComment))
            {
                var returnsEle = xmldoc.CreateElement("returns");
                returnsEle.InnerText = this.ReturnComment;
                root.AppendChild(returnsEle);
            }

           
            return root.ToXmlString();
        }

        public override string ToString()
        {
            return Name;
        }
    }

}
