using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.GenerateCode
{
    class CodeItem
    {
        CodeItem _parent;
        int _level;
        List<CodeItem> _Items;
        public string Body { get; }
        public CodeItem(string body)
        {
            this._Items = new List<CodeItem>();
            this.Body = body;
        }

        void SetParent(CodeItem parent)
        {
            _parent = parent;
            if (!string.IsNullOrEmpty(_parent.Body))
            {
                _level = parent._level + 1;
            }
        }

        public void AddItem(CodeItem item)
        {
            this._Items.Add(item);
            item.SetParent(this);
        }

        string getStartBlank()
        {
            return "".PadLeft(_level, '\t');
        }

        public string Build()
        {
           
            StringBuilder buffer = new StringBuilder();
            if (!string.IsNullOrEmpty(this.Body))
            {
                buffer.Append(getStartBlank());
                buffer.AppendLine(this.Body);
            }
            if (_Items.Count == 0)
            {
                return buffer.ToString();
            }

            buffer.Append(getStartBlank());
            buffer.AppendLine("{");

            foreach(var item in _Items )
            {
                buffer.Append(item.Build());
            }
            buffer.AppendLine("");
            buffer.Append(getStartBlank());
            buffer.AppendLine("}");

            return buffer.ToString();
        }
    }
}
