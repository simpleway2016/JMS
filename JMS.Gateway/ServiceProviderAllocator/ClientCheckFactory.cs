using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    internal class ClientCheckFactory
    {
        static ConcurrentDictionary<string, IClientCheck> CheckerDict = new ConcurrentDictionary<string, IClientCheck>();
        public IClientCheck Create(string code)
        {
            if (CheckerDict.ContainsKey(code))
                return CheckerDict[code];

            string text = @"
using System;
using System.Collections.Generic;

    public class Test : JMS.IClientCheck
    {
        public bool Check(IDictionary<string,string> headers)
        {
            " + code + @"
            return false;
        }
    }


return new Test();
";

            var op = ScriptOptions.Default.WithReferences(typeof(JMS.IClientCheck).Assembly);
            return CheckerDict[code] = CSharpScript.Create<IClientCheck>(text , op  ).RunAsync().GetAwaiter().GetResult().ReturnValue;
        }
    }
}
