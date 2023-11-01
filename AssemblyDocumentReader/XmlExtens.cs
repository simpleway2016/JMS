using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace JMS.AssemblyDocumentReader
{
    internal static class XmlExtens
    {
        public static string ToCommentString(this XmlNode xmlNode)
        {
            var innerText = xmlNode.InnerText;
            if(string.IsNullOrWhiteSpace(innerText))
            {
                return "";
            }
            try
            {
                var arr = innerText.Replace("\r", "").Split('\n').ToArray();
                while (arr[0].Length == 0)
                    arr = arr.Skip(1).ToArray();


                //看看第一行前面有几个空格
                if (arr.Length == 0)
                    return "";

                var m = Regex.Match(arr[0], @"^([ ]+)");
                var spaceCount = m.Length;
                for (int i = 0; i < arr.Length; i++)
                {
                    var line = arr[i];
                    int startIndex = 0;
                    for (int j = 0; j < spaceCount; j++)
                    {
                        if (line[j] == ' ')
                        {
                            startIndex = j + 1;
                        }
                    }
                    if (startIndex < line.Length)
                    {
                        arr[i] = line.Substring(startIndex);
                    }
                    else
                    {
                        arr[i] = "";
                    }
                }

                while (arr.Length > 0 && arr[arr.Length - 1].Length == 0)
                    arr = arr.Take(arr.Length - 1).ToArray();

                return string.Join("\r\n", arr);
            }
            catch  
            {
                return innerText.Trim();
            }
        }

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
