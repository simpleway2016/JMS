using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    public class ClientCheckProxy:IDisposable
    {
        AssemblyCSharpBuilder _assemblyCSharpBuilder;
        public string ClientCode { get; set; }

        IClientCheck _clientCheck;
        public ClientCheckProxy(IClientCheck clientCheck, AssemblyCSharpBuilder assemblyCSharpBuilder,string code)
        {
            this._assemblyCSharpBuilder = assemblyCSharpBuilder;
            this._clientCheck = clientCheck;
            ClientCode = code;
        }

        public bool Check(IDictionary<string, string> headers)
        {
            return _clientCheck.Check(headers);
        }

        public void Dispose()
        {
            if(_assemblyCSharpBuilder != null)
            {
                _clientCheck = null;
                try
                {
                    _assemblyCSharpBuilder.Domain.Unload();
                }
                catch 
                {
                }
                _assemblyCSharpBuilder = null;                
            }
        }
    }
}
