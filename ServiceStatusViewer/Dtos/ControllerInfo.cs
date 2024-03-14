using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.GenerateCode
{
    public class ControllerInfo
    {
        public string name { get; set; }
        public string desc { get; set; }
        public List<MethodItemInfo> items { get; set; }
        public List<DataTypeInfo> dataTypeInfos { get; set; }
    }

    public class DataTypeInfo
    {
        [Newtonsoft.Json.JsonIgnore]
        public Type type { get; set; }
        public string typeName { get; set; }
        public bool isEnum { get; set; }
        public List<ParameterInformation> members { get; set; }

        public DataTypeInfo(Type type)
        {
            this.type = type;
        }
    }

    public class MethodItemInfo
    {
        public string title { get; set; }
        public string method { get; set; }
        public string desc { get; set; }

        public DataBodyInfo data { get; set; }
        public DataBodyInfo returnData { get; set; }

    }
    public class ParameterInformation
    {
        public string name { get; set; }
        public string desc { get; set; }
        public string type { get; set; }
        public bool isNullable { get; set; }
    }
    public class DataBodyInfo
    {
        public string type { get; set; }
        public string desc { get; set; }
        public List<ParameterInformation> items { get; set; }

    }
}
