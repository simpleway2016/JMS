using JMS.Dtos;
using Microsoft.Extensions.Logging;
using Natasha.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    class ServiceRunningItem
    {
        ILogger _logger;

        public ServiceRunningItem(ILogger logger)
        {
            this._logger = logger;

        }

        private RegisterServiceInfo _ServiceInfo;
        public RegisterServiceInfo ServiceInfo
        {
            get => _ServiceInfo;
            set
            {
                _ServiceInfo = value;

                if (!string.IsNullOrEmpty(value.ClientCheckCode))
                {
                    if (this.ClientChecker != null && this.ClientChecker.ClientCode == value.ClientCheckCode)
                    {

                    }
                    else
                    {
                        try
                        {
                            string text = @"
using System;
using System.Collections.Generic;
namespace HelloWorld
{
    public class Test : JMS.IClientCheck
    {
        public AssemblyCSharpBuilder CodeBuilder { get; set; }
        public string ClientCode { get; set; }
        public bool Check(IDictionary<string,string> headers)
        {
            try{
                " + value.ClientCheckCode + @"
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

                            _logger?.LogInformation("客户端检验代码编译通过，代码：{0}", value.ClientCheckCode);

                            if (this.ClientChecker != null)
                            {
                                try
                                {
                                    this.ClientChecker.CodeBuilder.Domain.Unload();
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, "ClientChecker Unload失败");
                                }
                            }


                            this.ClientChecker = (IClientCheck)Activator.CreateInstance(type);
                            this.ClientChecker.ClientCode = value.ClientCheckCode;
                            this.ClientChecker.CodeBuilder = oop;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "客户端检验代码编译错误，代码：{0}", value.ClientCheckCode);
                        }
                    }
                }
                else
                {
                    if (this.ClientChecker != null)
                    {
                        try
                        {
                            this.ClientChecker.CodeBuilder.Domain.Unload();
                        }
                        catch
                        {
                        }
                    }
                    this.ClientChecker = null;
                }
            }
        }

        public IClientCheck ClientChecker { get; private set; }

    }
}
