using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace JMS.AssemblyDocumentReader
{
    internal class XmlNodeFactory
    {
        static ConcurrentDictionary<string, XmlNode> CacheList = new ConcurrentDictionary<string, XmlNode>();
        public static XmlNode GetTypeXmlNode(Type type)
        {            
            var xmlpath = $"{Path.GetDirectoryName(type.Assembly.Location)}/{Path.GetFileNameWithoutExtension(type.Assembly.Location)}.xml";

            return CacheList.GetOrAdd(xmlpath, (p) => {
                System.Xml.XmlDocument xmldoc = null;
                if (File.Exists(xmlpath))
                {
                    xmldoc = new System.Xml.XmlDocument();
                    xmldoc.Load(xmlpath);
                }
                else
                {
                    xmldoc = new XmlDocument();
                    xmldoc.LoadXml(@"<?xml version=""1.0""?><doc><members></members></doc>");
                }
                var ret = xmldoc.DocumentElement.SelectSingleNode("members");
                if (ret == null)
                    return xmldoc.CreateElement("members");

                return ret;
            });
        }

    }
}
