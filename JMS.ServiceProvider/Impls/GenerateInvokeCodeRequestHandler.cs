using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS.Impls
{
    class GenerateInvokeCodeRequestHandler : IRequestHandler
    {
        public InvokeType MatchType => InvokeType.GenerateInvokeCode;
        ICodeBuilder _codeBuilder;
        public GenerateInvokeCodeRequestHandler(ICodeBuilder codeBuilder)
        {
            _codeBuilder = codeBuilder;
        }

        public void Handle(NetStream netclient, InvokeCommand cmd)
        {
            var code = _codeBuilder.GenerateCode(cmd.Parameters[0],cmd.Parameters[1], cmd.Service);
            netclient.WriteServiceData(new InvokeResult()
            {
                Data = code,
                Success = true
            });
        }
    }
}
