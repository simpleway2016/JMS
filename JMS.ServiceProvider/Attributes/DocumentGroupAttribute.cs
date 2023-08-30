using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    public class DocumentGroupAttribute : Attribute
    {
        public DocumentGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }

        public string GroupName { get; }
    }
}
