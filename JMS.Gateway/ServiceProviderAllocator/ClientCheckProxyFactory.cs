using Natasha.CSharp;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    internal class ClientCheckProxyFactory
    {
        public ClientCheckProxy Create(string code)
        {
            string text = @"
using System;
using System.Collections.Generic;
namespace Codes" + DateTime.Now.Ticks + @"
{
    public class Test : JMS.IClientCheck
    {
        public bool Check(IDictionary<string,string> headers)
        {
            try{
                " + code + @"
            }
            catch{}
            return false;
        }
    }
}";

            //根据脚本创建动态类
            AssemblyCSharpBuilder oop = new AssemblyCSharpBuilder();
            oop.ThrowCompilerError();
            oop.ThrowSyntaxError();
            oop.Compiler.Domain = DomainManagement.Random;
            oop.Add(text);
            Type type = oop.GetTypeFromShortName("Test");

            return new ClientCheckProxy((IClientCheck)Activator.CreateInstance(type), oop , code);
        }
    }
}
