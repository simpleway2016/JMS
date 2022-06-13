using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace JMS.AssemblyDocumentReader
{
    internal static class XmlExtens
    {
        public static string ToXmlString(this XmlElement element)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                using (XmlTextWriter writer = new XmlTextWriter(ms, Encoding.UTF8)
                {
                    Formatting = Formatting.Indented//缩进
                })
                {
                    element.WriteContentTo(writer);
                    writer.Flush();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }
}
