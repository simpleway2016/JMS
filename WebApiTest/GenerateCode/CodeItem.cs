using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace WebApiTest.GenerateCode
{
    class CodeItem
    {
        protected CodeItem Parent { get; set; }
        List<CodeItem> _Items;
        public string Body { get; set; }
        public List<string> Attributes { get; }
        public string Comment { get; set; }
        public CodeItem(string body)
        {
            this._Items = new List<CodeItem>();
            this.Body = body;
            this.Comment = "";
            this.Attributes = new List<string>();
        }
        public CodeItem()
        {
            this._Items = new List<CodeItem>();
            this.Comment = "";
            this.Attributes = new List<string>();
        }
        public void ClearItems()
        {
            _Items.Clear();
        }
        public int GetItemCount() => _Items.Count;
        void SetParent(CodeItem parent)
        {
            Parent = parent;
        }

        public CodeItem AddItem(CodeItem item)
        {
            this._Items.Add(item);
            item.SetParent(this);
            return this;
        }

        public CodeItem AddString(string text)
        {
            this.AddItem(new CodeItem(text));
            return this;
        }

        string getStartBlank()
        {
            int level = 0;
            var p = this.Parent;
            while(p != null)
            {
                level++;
                p = p.Parent;
            }
            return "".PadLeft(level * 4, ' ');
        }

        protected void AppendIndentLine(StringBuilder buffer,string line)
        {
            buffer.Append(getStartBlank());
            buffer.AppendLine(line);
        }

        public virtual string Build()
        {
           
            StringBuilder buffer = new StringBuilder();
            if(!string.IsNullOrEmpty(this.Comment))
            {
                var comments = this.Comment.Split('\n').Select(m => m.Trim()).ToArray();

                foreach( var line in comments )
                {
                    AppendIndentLine(buffer, line);
                }
            }
            if(this.Attributes.Count > 0)
            {
                foreach( var attr in Attributes )
                {
                    AppendIndentLine(buffer, attr);
                }
            }
            if (!string.IsNullOrEmpty(this.Body))
            {
                AppendIndentLine(buffer, this.Body);
            }
            if (_Items.Count == 0)
            {
                return buffer.ToString();
            }

            if (!string.IsNullOrEmpty(this.Body))
            {
                AppendIndentLine(buffer, "{");
            }

            foreach(var item in _Items )
            {
                buffer.Append(item.Build());
            }

            if (!string.IsNullOrEmpty(this.Body))
            {
                AppendIndentLine(buffer, "}");
            }

            return buffer.ToString();
        }
    }

    class BeforeCodeItem:CodeItem
    {
        public string CodeEnd { get; set; }
        List<string> _beforeCodes = new List<string>();
        public BeforeCodeItem():base()
        {

        }
        public BeforeCodeItem(string body) : base(body)
        {

        }
        public void AddBeforeCode(string code)
        {
            _beforeCodes.Add(code);
        }

        public override string Build()
        {
            StringBuilder buffer = new StringBuilder();
            foreach( var str in _beforeCodes )
            {
                AppendIndentLine(buffer, str);
            }
            buffer.Append( base.Build());
            if (!string.IsNullOrEmpty(this.CodeEnd))
            {
                buffer.AppendLine("");
                buffer.AppendLine(this.CodeEnd);
            }
            return buffer.ToString();
        }
    }
    class NamespaceCode : BeforeCodeItem
    {

        private string _NameSpace;
        public string NameSpace
        {
            get => _NameSpace;
            set
            {
                if (_NameSpace != value)
                {
                    _NameSpace = value;
                    this.Body = $"namespace {value}";
                }
            }
        }
        public NamespaceCode(string nameSpace) : base()
        {
            this.NameSpace = nameSpace;
        }
        public void AddUsing(string code)
        {
            this.AddBeforeCode($"using {code};");
        }
         
    }
    class GetSetCodeItem:CodeItem
    {
        public GetSetCodeItem(string body):base(body)
        {

        }

        public override string Build()
        {
            if(this.GetItemCount() == 0)
            {
                StringBuilder buffer = new StringBuilder();
                AppendIndentLine(buffer, this.Body + ";");
                return buffer.ToString();
            }
            else
            {
                return base.Build();
            }
        }
    }

    class PropertyCodeItem:CodeItem
    {
        public string PropertyName { get; set; }
        public string PropertyType { get; set; }
        /// <summary>
        /// 修饰语，如 public virtual
        /// </summary>
        public string Modification { get; set; }
        /// <summary>
        /// get的描述 默认字符串：get，如果null，表示没有get
        /// </summary>
        public CodeItem ItemForGet { get; set; }
        /// <summary>
        /// set的描述 默认字符串：set，如果null，表示没有set
        /// </summary>
        public CodeItem ItemForSet { get; set; }
        public PropertyCodeItem(string propertyName) : base()
        {
            this.PropertyName = propertyName;
            this.ItemForGet = new GetSetCodeItem("get");
            this.ItemForSet = new GetSetCodeItem("set");
        }

        public override string Build()
        {
            this.Body = $"{(this.Modification==null?"":this.Modification + " ")}{this.PropertyType} {this.PropertyName}";
            this.ClearItems();
            if(ItemForGet != null)
            {
                this.AddItem(ItemForGet);
            }
            if (ItemForSet != null)
            {
                this.AddItem(ItemForSet);
            }
            return base.Build();
        }
    }

    class FieldCodeItem : CodeItem
    {
        public string FieldName { get; set; }
        public string FieldType { get; set; }
        /// <summary>
        /// 修饰语，如 public virtual
        /// </summary>
        public string Modification { get; set; }
        public FieldCodeItem(string fieldName) : base()
        {
            this.FieldName = fieldName;
        }

        public override string Build()
        {
            this.Body = $"{(this.Modification == null ? "" : this.Modification + " ")}{this.FieldType} {this.FieldName};";
            this.ClearItems();
            return base.Build();
        }
    }
}
